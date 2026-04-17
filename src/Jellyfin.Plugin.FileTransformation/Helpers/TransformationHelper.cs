using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.FileTransformation.Helpers
{
    public static class TransformationHelper
    {
        private static readonly HttpClient s_sharedHttpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        public static async Task ApplyTransformation(string path, Stream contents, TransformationRegistrationPayload payload, ILogger logger, IServerApplicationHost serverApplicationHost)
        {
            logger.LogDebug($"[FileTransformation] Transformation requested for {path}");

            // Validate that at least one callback mechanism is configured
            if (payload.CallbackAssembly == null && payload.TransformationPipe == null
                && string.IsNullOrEmpty(payload.TransformationEndpoint))
            {
                logger.LogWarning($"[FileTransformation] No callback configured for {path}, skipping");
                return;
            }

            using StreamReader reader = new StreamReader(contents, leaveOpen: true);
            JObject obj = new JObject { { "contents", await reader.ReadToEndAsync() } };

            string? transformedString = null;

            // Tier 1: Assembly reflection callback
            if (payload.CallbackAssembly != null)
            {
                try
                {
                    Assembly? assembly = AssemblyLoadContext.All
                        .FirstOrDefault(x => x.Assemblies.Select(y => y.FullName).Contains(payload.CallbackAssembly))?
                        .Assemblies.FirstOrDefault(x => x.FullName == payload.CallbackAssembly);

                    if (assembly == null)
                    {
                        logger.LogWarning($"[FileTransformation] Assembly '{payload.CallbackAssembly}' not found for {path}");
                        return;
                    }

                    Type? type = assembly.GetType(payload.CallbackClass!);
                    if (type == null)
                    {
                        logger.LogWarning($"[FileTransformation] Type '{payload.CallbackClass}' not found in assembly for {path}");
                        return;
                    }

                    MethodInfo? method = type.GetMethod(payload.CallbackMethod!);
                    if (method == null)
                    {
                        logger.LogWarning($"[FileTransformation] Method '{payload.CallbackMethod}' not found on type for {path}");
                        return;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        logger.LogWarning($"[FileTransformation] Callback method has no parameters for {path}");
                        return;
                    }

                    object? paramObj = obj.ToObject(parameters[0].ParameterType);
                    transformedString = method.Invoke(null, [paramObj]) as string;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"[FileTransformation] Assembly callback failed for {path}");
                    return;
                }
            }

            // Tier 2: Named pipe
            if (transformedString == null && payload.TransformationPipe != null)
            {
                try
                {
                    await using NamedPipeClientStream pipe = new NamedPipeClientStream(".", payload.TransformationPipe, PipeDirection.InOut);
                    await pipe.ConnectAsync(5000);

                    byte[] payloadBytes = Encoding.UTF8.GetBytes(obj.ToString(Formatting.None));
                    byte[] payloadLengthBytes = BitConverter.GetBytes((long)payloadBytes.Length);
                    await pipe.WriteAsync(payloadLengthBytes);
                    await pipe.WriteAsync(payloadBytes);

                    byte[] lengthBuffer = new byte[8];
                    await pipe.ReadExactlyAsync(lengthBuffer, 0, lengthBuffer.Length);
                    long length = BitConverter.ToInt64(lengthBuffer, 0);

                    if (length <= 0 || length > 50 * 1024 * 1024)
                    {
                        logger.LogError($"[FileTransformation] Invalid pipe response length ({length} bytes) for {path}");
                        return;
                    }

                    using MemoryStream memoryStream = new MemoryStream();
                    while (length > 0)
                    {
                        byte[] buffer = new byte[Math.Min(8192, length)];
                        int bytesRead = await pipe.ReadAsync(buffer);
                        if (bytesRead == 0)
                        {
                            throw new EndOfStreamException($"Pipe closed prematurely, {length} bytes remaining");
                        }

                        length -= bytesRead;
                        memoryStream.Write(buffer, 0, bytesRead);
                    }

                    transformedString = Encoding.UTF8.GetString(memoryStream.ToArray());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"[FileTransformation] Named pipe callback failed for {path}");
                    return;
                }
            }

            // Tier 3: HTTP endpoint
            if (transformedString == null && !string.IsNullOrEmpty(payload.TransformationEndpoint))
            {
                try
                {
                    string requestUri = payload.TransformationEndpoint;
                    if (!requestUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        && !requestUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        requestUri = $"http://localhost:{serverApplicationHost.HttpPort}{payload.TransformationEndpoint}";
                    }

                    HttpResponseMessage response = await s_sharedHttpClient
                        .PostAsync(requestUri, new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json"));

                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogError($"[FileTransformation] HTTP callback returned {(int)response.StatusCode} for {path}");
                        return;
                    }

                    transformedString = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"[FileTransformation] HTTP callback failed for {path}");
                    return;
                }
            }

            if (transformedString == null)
            {
                return;
            }

            // Write result back to stream — truncate to avoid stale trailing bytes
            contents.Seek(0, SeekOrigin.Begin);
            using StreamWriter textWriter = new StreamWriter(contents, null, -1, leaveOpen: true);
            textWriter.Write(transformedString);
            await textWriter.FlushAsync();
            contents.SetLength(contents.Position);
        }
    }
}
