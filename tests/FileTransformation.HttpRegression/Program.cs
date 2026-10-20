using System.IO.Compression;
using System.Net;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Infrastructure;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

internal static class Program
{
    private static async Task<int> Main()
    {
        using Fixture fixture = new Fixture();
        (string Name, Func<Task> Run)[] tests =
        [
            ("GET returns the complete transformed representation", async () =>
            {
                Reply reply = await fixture.Request();
                Equal(200, reply.Status);
                Equal(Fixture.Transformed, reply.Text);
                Equal(reply.Body.LongLength.ToString(), reply.Header(HeaderNames.ContentLength));
                Equal("no-cache", reply.Header(HeaderNames.CacheControl));
            }),
            ("HEAD has the GET headers and no body", async () =>
            {
                Reply get = await fixture.Request();
                Reply head = await fixture.Request(method: "HEAD");
                SameRepresentation(get, head);
                Equal(0, head.Body.Length);
                Equal("HEAD", head.Method);
            }),
            ("compressed HEAD has the compressed GET length and no body", async () =>
            {
                Reply get = await fixture.Request(encoding: "gzip", local: false);
                Reply head = await fixture.Request(method: "HEAD", encoding: "gzip", local: false);
                SameRepresentation(get, head);
                Equal("gzip", get.Header(HeaderNames.ContentEncoding));
                Equal(0, head.Body.Length);
                using GZipStream gzip = new GZipStream(new MemoryStream(get.Body), CompressionMode.Decompress);
                using StreamReader reader = new StreamReader(gzip);
                Equal(Fixture.Transformed, await reader.ReadToEndAsync());
            }),
            ("open, partial and unsatisfiable ranges receive the full transformed file", async () =>
            {
                foreach (string range in new[] { "bytes=0-", "bytes=0-7", "bytes=999999-" })
                {
                    Reply reply = await fixture.Request(range: range);
                    Equal(200, reply.Status);
                    Equal(Fixture.Transformed, reply.Text);
                    Equal(string.Empty, reply.Header(HeaderNames.ContentRange));
                    Equal(string.Empty, reply.Header(HeaderNames.AcceptRanges));
                }
            }),
            ("If-Range cannot select an original-file range", async () =>
            {
                Reply original = await fixture.Request(path: "/web/plain.js");
                Reply reply = await fixture.Request(range: "bytes=0-7", ifRange: original.Header(HeaderNames.ETag));
                Equal(200, reply.Status);
                Equal(Fixture.Transformed, reply.Text);
            }),
            ("HEAD with a range has the full transformed GET headers", async () =>
            {
                Reply get = await fixture.Request();
                Reply head = await fixture.Request(method: "HEAD", range: "bytes=0-7");
                SameRepresentation(get, head);
                Equal(0, head.Body.Length);
            }),
            ("the transformed ETag still gives a bodyless 304 for GET and HEAD", async () =>
            {
                Reply get = await fixture.Request();
                foreach (string method in new[] { "GET", "HEAD" })
                {
                    Reply reply = await fixture.Request(method: method, etag: get.Header(HeaderNames.ETag));
                    Equal(304, reply.Status);
                    Equal(get.Header(HeaderNames.ETag), reply.Header(HeaderNames.ETag));
                    Equal(0, reply.Body.Length);
                }
            }),
            ("a stale transformed ETag receives the current transformed content", async () =>
            {
                Reply reply = await fixture.Request(etag: "\"old-transformed-content\"");
                Equal(200, reply.Status);
                Equal(Fixture.Transformed, reply.Text);
            }),
            ("unregistered files keep native HEAD and range behavior", async () =>
            {
                Reply get = await fixture.Request(path: "/web/plain.js");
                Reply head = await fixture.Request(path: "/web/plain.js", method: "HEAD");
                SameRepresentation(get, head);
                Equal(0, head.Body.Length);
                Reply range = await fixture.Request(path: "/web/plain.js", range: "bytes=0-7");
                Equal(206, range.Status);
                Equal(Fixture.Original[..8], range.Text);
            }),
            ("virtual HEAD describes the synthesized GET representation", async () =>
            {
                Reply get = await fixture.Request(path: "/web/virtual.js");
                Reply head = await fixture.Request(path: "/web/virtual.js", method: "HEAD");
                Equal("virtual content", get.Text);
                SameRepresentation(get, head);
                Equal(0, head.Body.Length);
            }),
            ("empty virtual files remain 404 for GET and HEAD", async () =>
            {
                foreach (string method in new[] { "GET", "HEAD" })
                {
                    Reply reply = await fixture.Request(path: "/web/empty.js", method: method);
                    Equal(404, reply.Status);
                    Equal(0, reply.Body.Length);
                }
            }),
            ("non-GET/HEAD methods are not transformed or synthesized", async () =>
            {
                int before = fixture.Transforms.Calls;
                foreach (string method in new[] { "POST", "OPTIONS" })
                {
                    Reply reply = await fixture.Request(method: method);
                    Equal(404, reply.Status);
                    Equal(method, reply.Method);
                }
                Equal(before, fixture.Transforms.Calls);
            }),
            ("non-Web requests retain their method, range and response", async () =>
            {
                DefaultHttpContext context = new DefaultHttpContext();
                context.Request.Path = "/Videos/example/stream";
                context.Request.Method = "GET";
                context.Request.Headers.Range = "bytes=0-7";
                context.Response.Body = new MemoryStream();
                FileTransformationMiddleware middleware = new FileTransformationMiddleware(async request =>
                {
                    Equal("GET", request.Request.Method);
                    Equal("bytes=0-7", request.Request.Headers.Range.ToString());
                    request.Response.StatusCode = 206;
                    await request.Response.WriteAsync("media bytes");
                });
                await middleware.InvokeAsync(context, fixture.Transforms, NullLogger<FileTransformationMiddleware>.Instance);
                Equal(206, context.Response.StatusCode);
                Equal(11L, context.Response.Body.Length);
            }),
            ("a downstream failure restores the HEAD method and original stream", async () =>
            {
                DefaultHttpContext context = new DefaultHttpContext();
                context.Request.Path = "/web/transformed.js";
                context.Request.Method = "HEAD";
                using MemoryStream body = new MemoryStream();
                context.Response.Body = body;
                FileTransformationMiddleware middleware = new FileTransformationMiddleware(_ => throw new IOException("downstream failure"));
                bool threw = false;
                try
                {
                    await middleware.InvokeAsync(context, fixture.Transforms, NullLogger<FileTransformationMiddleware>.Instance);
                }
                catch (IOException)
                {
                    threw = true;
                }
                Equal(true, threw);
                Equal("HEAD", context.Request.Method);
                Equal(true, ReferenceEquals(body, context.Response.Body));
                Equal(0L, body.Length);
            })
        ];

        int failures = 0;
        foreach ((string name, Func<Task> run) in tests)
        {
            try
            {
                await run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void SameRepresentation(Reply get, Reply head)
    {
        Equal(get.Status, head.Status);
        foreach (string header in new[] { HeaderNames.ContentLength, HeaderNames.ContentType, HeaderNames.ContentEncoding, HeaderNames.ETag, HeaderNames.CacheControl, HeaderNames.Vary })
        {
            Equal(get.Header(header), head.Header(header));
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}; got {actual}");
        }
    }

    private sealed record Reply(int Status, Dictionary<string, string> Headers, byte[] Body, string Method)
    {
        public string Text => Encoding.UTF8.GetString(Body);
        public string Header(string name) => Headers.GetValueOrDefault(name, string.Empty);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Original = "const value = 'original';\n";
        public const string Transformed = "const value = 'transformed response';\n";
        private readonly string m_root = Path.Combine(Path.GetTempPath(), "file-transformation-http-" + Guid.NewGuid().ToString("N"));
        private readonly PhysicalFileProvider m_files;
        private readonly FileTransformationMiddleware m_middleware;
        public TestTransforms Transforms { get; } = new TestTransforms();

        public Fixture()
        {
            Directory.CreateDirectory(m_root);
            File.WriteAllText(Path.Combine(m_root, "transformed.js"), Original);
            File.WriteAllText(Path.Combine(m_root, "plain.js"), Original);
            m_files = new PhysicalFileProvider(m_root);
            StaticFileMiddleware files = new StaticFileMiddleware(
                context => { context.Response.StatusCode = 404; return Task.CompletedTask; },
                new TestEnvironment(m_root, m_files),
                Options.Create(new StaticFileOptions { RequestPath = "/web", FileProvider = m_files }),
                NullLoggerFactory.Instance);
            m_middleware = new FileTransformationMiddleware(files.Invoke);
        }

        public async Task<Reply> Request(string path = "/web/transformed.js", string method = "GET", string? range = null, string? ifRange = null, string? etag = null, string? encoding = null, bool local = true)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Method = method;
            context.Connection.RemoteIpAddress = local ? IPAddress.Loopback : IPAddress.Parse("203.0.113.1");
            if (range != null)
            {
                context.Request.Headers.Range = range;
            }
            if (ifRange != null)
            {
                context.Request.Headers.IfRange = ifRange;
            }
            if (etag != null)
            {
                context.Request.Headers.IfNoneMatch = etag;
            }
            if (encoding != null)
            {
                context.Request.Headers.AcceptEncoding = encoding;
            }
            using MemoryStream body = new MemoryStream();
            context.Response.Body = body;
            await m_middleware.InvokeAsync(context, Transforms, NullLogger<FileTransformationMiddleware>.Instance);
            return new Reply(context.Response.StatusCode, context.Response.Headers.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase), body.ToArray(), context.Request.Method);
        }

        public void Dispose()
        {
            m_files.Dispose();
            Directory.Delete(m_root, recursive: true);
        }
    }

    private sealed class TestTransforms : IWebFileTransformationReadService
    {
        public int Calls { get; private set; }
        public bool NeedsTransformation(string path) => path is "transformed.js" or "virtual.js" or "empty.js";

        public async Task RunTransformation(string path, Stream stream)
        {
            Calls++;
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream, leaveOpen: true);
            string text = await reader.ReadToEndAsync();
            string transformed = path == "virtual.js" ? "virtual content" : text.Replace(Fixture.Original, Fixture.Transformed);
            stream.SetLength(0);
            stream.Position = 0;
            await stream.WriteAsync(Encoding.UTF8.GetBytes(transformed));
            stream.Position = 0;
        }
    }

    private sealed class TestEnvironment(string root, IFileProvider files) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FileTransformation.HttpRegression";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = files;
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = files;
    }
}
