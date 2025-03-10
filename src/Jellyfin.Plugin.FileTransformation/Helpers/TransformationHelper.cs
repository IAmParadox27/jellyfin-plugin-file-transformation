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
        public static async Task ApplyTransformation(string path, Stream contents, TransformationRegistrationPayload payload, ILogger logger, IServerApplicationHost serverApplicationHost)
        {
            logger.LogDebug($"Transformation requested for '{path}'");

            using StreamReader reader = new StreamReader(contents, leaveOpen: true);

            JObject obj = new JObject();
            obj.Add("contents", reader.ReadToEnd());
            
            string? transformedString = null;
            if (payload.CallbackAssembly != null)
            {
                Assembly? assembly = AssemblyLoadContext.All
                    .FirstOrDefault(x => x.Assemblies.Select(y => y.FullName).Contains(payload.CallbackAssembly))?
                    .Assemblies.FirstOrDefault(x => x.FullName == payload.CallbackAssembly);

                Type? type = assembly?.GetType(payload.CallbackClass!);

                MethodInfo? method = type?.GetMethod(payload.CallbackMethod!);

                if (method != null)
                {
                    ParameterInfo payloadParameter = method.GetParameters()[0];
                    object? paramObj = obj.ToObject(payloadParameter.ParameterType);
                    
                    transformedString = (string)method.Invoke(null, new object?[] { paramObj })!;
                }
            }
            
            if (transformedString == null && payload.TransformationPipe != null)
            {
                NamedPipeClientStream pipe = new NamedPipeClientStream(".", payload.TransformationPipe, PipeDirection.InOut);
                await pipe.ConnectAsync();
                
                byte[] payloadBytes = Encoding.UTF8.GetBytes(obj.ToString(Formatting.None));
                byte[] payloadLengthBytes = BitConverter.GetBytes((long)payloadBytes.Length);
                await pipe.WriteAsync(payloadLengthBytes, 0, 8);
                await pipe.WriteAsync(payloadBytes, 0, payloadBytes.Length);
                
                byte[] lengthBuffer = new byte[8];
                await pipe.ReadExactlyAsync(lengthBuffer, 0, lengthBuffer.Length);
                long length = BitConverter.ToInt64(lengthBuffer, 0);
                
                MemoryStream memoryStream = new MemoryStream();
                while (length > 0)
                {
                    byte[] buffer = new byte[Math.Min(1024, length)];
                    int bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length);
                    length -= bytesRead;
                    
                    memoryStream.Write(buffer, 0, bytesRead);
                }
                transformedString = Encoding.UTF8.GetString(memoryStream.ToArray());
            }
            
            if (transformedString == null)
            {
                HttpClient client = new HttpClient();
                if (!(payload.TransformationEndpoint.StartsWith("http") || payload.TransformationEndpoint.StartsWith("https")))
                {
                    string? publishedServerUrl = serverApplicationHost.GetType()
                        .GetProperty("PublishedServerUrl", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(serverApplicationHost) as string;
                    logger.LogTrace($"Retrieved value for published server URL: {publishedServerUrl}");
                
                    client.BaseAddress = new Uri(publishedServerUrl ?? $"http://localhost:{serverApplicationHost.HttpPort}");

                    logger.LogTrace($"Set base address to '{client.BaseAddress}'");
                }
                
                HttpResponseMessage responseMessage = await client
                    .PostAsync(payload.TransformationEndpoint, new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json"));
                transformedString = await responseMessage.Content.ReadAsStringAsync();
                
                logger.LogDebug($"Response for request '{responseMessage.RequestMessage?.RequestUri?.ToString()}' received from endpoint '{payload.TransformationEndpoint}' with code {responseMessage.StatusCode} {(int)responseMessage.StatusCode}");
            }
            
            contents.Seek(0, SeekOrigin.Begin);

            using StreamWriter textWriter = new StreamWriter(contents, null, -1, true);
            textWriter.Write(transformedString);
        }
    }
}