using System.Reflection;
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
            HttpClient client = new HttpClient();
            if (!(payload.TransformationEndpoint.StartsWith("http") || payload.TransformationEndpoint.StartsWith("https")))
            {
                string? publishedServerUrl = serverApplicationHost.GetType()
                    .GetProperty("PublishedServerUrl", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(serverApplicationHost) as string;
                logger.LogTrace($"Retrieved value for published server URL: {publishedServerUrl}");
            
                client.BaseAddress = new Uri(publishedServerUrl ?? $"http://localhost:{serverApplicationHost.HttpPort}");

                logger.LogTrace($"Set base address to '{client.BaseAddress}'");
            }
            
            using StreamReader reader = new StreamReader(contents, leaveOpen: true);
            
            JObject obj = new JObject();
            obj.Add("contents", reader.ReadToEnd());
            
            HttpResponseMessage responseMessage = await client
                .PostAsync(payload.TransformationEndpoint, new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json"));
            string transformedString = await responseMessage.Content.ReadAsStringAsync();
            
            logger.LogDebug($"Response for request '{responseMessage.RequestMessage?.RequestUri?.ToString()}' received from endpoint '{payload.TransformationEndpoint}' with code {responseMessage.StatusCode} {(int)responseMessage.StatusCode}");
            
            contents.Seek(0, SeekOrigin.Begin);

            using StreamWriter textWriter = new StreamWriter(contents, null, -1, true);
            textWriter.Write(transformedString);
        }
    }
}