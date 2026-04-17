using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Configuration;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public sealed class FileTransformationMiddleware
    {
        private readonly RequestDelegate m_next;

        private string GetAutoRefreshScript(string mode, bool debug)
        {
            string resourcePath = $"{typeof(FileTransformationPlugin).Namespace}.Script.auto-refresh.js";
            Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                return string.Empty;
            }

            using StreamReader reader = new StreamReader(stream);
            string script = reader.ReadToEnd();
            return script.Replace("__FT_MODE__", mode).Replace("__FT_DEBUG__", debug ? "true" : "false");
        }

        public FileTransformationMiddleware(RequestDelegate next)
        {
            m_next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IWebFileTransformationReadService readService,
            ILogger<FileTransformationMiddleware> logger)
        {
            string path = context.Request.Path.Value ?? string.Empty;

            // Fast path: only intercept /web/ paths.
            // Also handle /web (no trailing slash) which Jellyfin redirects.
            int webIndex = path.IndexOf("/web/", StringComparison.OrdinalIgnoreCase);
            if (webIndex < 0 && !path.EndsWith("/web", StringComparison.OrdinalIgnoreCase))
            {
                await m_next(context);
                return;
            }

            // Extract the relative path after /web/
            // When path is "/web/" or "/web", resolve to "index.html" since
            // Jellyfin's UseDefaultFiles serves index.html for the root.
            string relativePath = webIndex >= 0 ? path.Substring(webIndex + 5) : string.Empty;
            if (string.IsNullOrEmpty(relativePath))
            {
                relativePath = "index.html";
            }

            // Only intercept index.html for auto-refresh script injection when the feature is enabled.
            // For other paths, only intercept if a transformation is registered.
            ConfigChangeNotification notification = FileTransformationPlugin.Instance?.Configuration?.ConfigChangeNotification
                ?? ConfigChangeNotification.Toast;
            bool isIndexHtml = string.Equals(relativePath, "index.html", StringComparison.OrdinalIgnoreCase)
                && notification != ConfigChangeNotification.Disabled;
            bool needsTransform = readService.NeedsTransformation(relativePath);

            if (!isIndexHtml && !needsTransform)
            {
                await m_next(context);
                return;
            }

            logger.LogDebug($"[FileTransformation] Intercepting response for: {relativePath}");

            // Save the client's Accept-Encoding before stripping — we'll re-compress after transforming.
            string acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();

            // Strip Accept-Encoding so Jellyfin serves uncompressed content into our buffer.
            context.Request.Headers.Remove("Accept-Encoding");

            // Buffer the response body so we can run transformations on it
            Stream originalBody = context.Response.Body;
            using MemoryStream bufferedBody = new MemoryStream();
            context.Response.Body = bufferedBody;

            try
            {
                await m_next(context);

                // Virtual file synthesis: if a transform is registered for a file that
                // doesn't exist on disk (404), create an empty response and let the
                // transform callbacks generate the content. This is how Plugin Pages
                // creates virtual pages like userpluginsettings.html.
                bool isSynthesized = false;
                if (context.Response.StatusCode == 404 && needsTransform)
                {
                    logger.LogDebug($"[FileTransformation] Synthesizing virtual file for '{relativePath}'");
                    isSynthesized = true;
                    bufferedBody.SetLength(0);
                    context.Response.StatusCode = 200;

                    FileExtensionContentTypeProvider contentTypeProvider = new FileExtensionContentTypeProvider();
                    if (contentTypeProvider.TryGetContentType(relativePath, out string? contentType))
                    {
                        context.Response.ContentType = contentType;
                    }
                    else
                    {
                        context.Response.ContentType = "application/octet-stream";
                    }
                }
                else if (context.Response.StatusCode != 200)
                {
                    // Non-200 response with no transform registered — pass through
                    bufferedBody.Seek(0, SeekOrigin.Begin);
                    context.Response.Body = originalBody;
                    await bufferedBody.CopyToAsync(context.Response.Body);
                    return;
                }

                // Run the transformation pipeline (only if transforms are registered)
                if (needsTransform)
                {
                    try
                    {
                        await readService.RunTransformation(relativePath, bufferedBody);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"[FileTransformation] Transformation pipeline failed for '{relativePath}'. Serving original content.");
                    }

                    // If this was a synthesized virtual file and the transforms produced nothing,
                    // revert to 404 instead of serving an empty 200.
                    if (isSynthesized && bufferedBody.Length == 0)
                    {
                        logger.LogWarning($"[FileTransformation] Virtual file synthesis for '{relativePath}' produced empty content, reverting to 404");
                        context.Response.StatusCode = 404;
                        context.Response.ContentLength = 0;
                        context.Response.Body = originalBody;
                        return;
                    }
                }

                // For index.html, inject the auto-refresh script after all transforms
                if (isIndexHtml)
                {
                    try
                    {
                        await InjectAutoRefreshScript(bufferedBody);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[FileTransformation] Failed to inject auto-refresh script");
                    }
                }

                // Compute ETag from the transformed content so browsers can do conditional requests.
                // This brings back 304 responses for unchanged transformed content.
                bufferedBody.Seek(0, SeekOrigin.Begin);
                byte[] hashBytes = await SHA256.HashDataAsync(bufferedBody);
                string etag = $"\"{BitConverter.ToString(hashBytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant()}\"";

                // Check If-None-Match — return 304 if content hasn't changed
                string ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
                if (!string.IsNullOrEmpty(ifNoneMatch) && string.Equals(ifNoneMatch, etag, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 304;
                    context.Response.ContentLength = 0;
                    context.Response.Headers[HeaderNames.ETag] = etag;
                    context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
                    context.Response.Body = originalBody;
                    return;
                }

                // Prepare the response headers
                context.Response.Headers.Remove("Content-Encoding");
                context.Response.Headers.Remove("Last-Modified");
                context.Response.Headers[HeaderNames.ETag] = etag;
                context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
                context.Response.Headers[HeaderNames.Vary] = "Accept-Encoding";

                // Re-compress the transformed content if the client supports it.
                // Skip compression for local/LAN clients where it just adds CPU overhead.
                bufferedBody.Seek(0, SeekOrigin.Begin);
                context.Response.Body = originalBody;

                // Parse Accept-Encoding tokens properly — honor q=0 (means "not supported")
                bool isLocal = IsLocalRequest(context);
                HashSet<string> encodings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string entry in acceptEncoding.Split(','))
                {
                    string trimmed = entry.Trim();
                    string[] parts = trimmed.Split(';');
                    string token = parts[0].Trim();
                    bool rejected = false;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string param = parts[i].Trim();
                        if (param.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                        {
                            string qVal = param.Substring(2).Trim();
                            // InvariantCulture is required here — HTTP quality values always use '.' as
                            // the decimal separator per RFC 7231, but the default TryParse overload uses
                            // the server's current culture which may use ',' (e.g. de-DE, fr-FR).
                            if (float.TryParse(qVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float quality) && quality <= 0f)
                            {
                                rejected = true;
                            }

                            break;
                        }
                    }

                    if (!rejected && token.Length > 0)
                    {
                        encodings.Add(token);
                    }
                }

                string? selectedEncoding = isLocal ? null : SelectEncoding(encodings);
                if (selectedEncoding != null)
                {
                    using MemoryStream compressed = new MemoryStream();
                    using (Stream compressor = CreateCompressionStream(selectedEncoding, compressed))
                    {
                        await bufferedBody.CopyToAsync(compressor);
                    }

                    context.Response.Headers[HeaderNames.ContentEncoding] = selectedEncoding;
                    context.Response.ContentLength = compressed.Length;
                    compressed.Seek(0, SeekOrigin.Begin);
                    await compressed.CopyToAsync(originalBody);
                }
                else
                {
                    context.Response.ContentLength = bufferedBody.Length;
                    await bufferedBody.CopyToAsync(originalBody);
                }
            }
            catch
            {
                context.Response.Body = originalBody;
                throw;
            }
        }

        private string? SelectEncoding(HashSet<string> encodings)
        {
            if (encodings.Contains("br"))
            {
                return "br";
            }

            if (encodings.Contains("gzip"))
            {
                return "gzip";
            }

            return null;
        }

        private Stream CreateCompressionStream(string encoding, Stream output)
        {
            return encoding switch
            {
                "br" => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
                "gzip" => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
                _ => throw new ArgumentException($"Unsupported encoding: {encoding}", nameof(encoding)),
            };
        }

        private bool IsLocalRequest(HttpContext context)
        {
            IPAddress? remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                return true;
            }

            // Handle IPv6-mapped IPv4 (e.g. ::ffff:192.168.1.5 from Docker/dual-stack)
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            // Loopback (127.0.0.1, ::1)
            if (IPAddress.IsLoopback(remoteIp))
            {
                return true;
            }

            // Same machine (remote == local)
            IPAddress? localIp = context.Connection.LocalIpAddress;
            if (localIp != null)
            {
                if (localIp.IsIPv4MappedToIPv6)
                {
                    localIp = localIp.MapToIPv4();
                }

                if (remoteIp.Equals(localIp))
                {
                    return true;
                }
            }

            // Private/LAN ranges: 10.x, 172.16-31.x, 192.168.x
            byte[] bytes = remoteIp.GetAddressBytes();
            if (bytes.Length == 4)
            {
                if (bytes[0] == 10)
                {
                    return true;
                }

                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {
                    return true;
                }

                if (bytes[0] == 192 && bytes[1] == 168)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task InjectAutoRefreshScript(MemoryStream body)
        {
            FileTransformationPlugin? plugin = FileTransformationPlugin.Instance;
            ConfigChangeNotification notification = plugin?.Configuration?.ConfigChangeNotification
                               ?? ConfigChangeNotification.Toast;

            if (notification == ConfigChangeNotification.Disabled)
            {
                return;
            }

            string mode = notification == ConfigChangeNotification.AutoReload ? "reload" : "toast";
            bool debug = plugin?.Configuration?.DebugLoggingState == DebugLoggingState.Enabled;

            body.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: -1, leaveOpen: true);
            string html = await reader.ReadToEndAsync();

            int insertPoint = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (insertPoint < 0)
            {
                return;
            }

            string script = GetAutoRefreshScript(mode, debug);
            if (string.IsNullOrEmpty(script))
            {
                return;
            }

            string modified = string.Concat(html.AsSpan(0, insertPoint), script, html.AsSpan(insertPoint));
            byte[] bytes = Encoding.UTF8.GetBytes(modified);

            body.SetLength(0);
            body.Seek(0, SeekOrigin.Begin);
            await body.WriteAsync(bytes);
        }
    }
}
