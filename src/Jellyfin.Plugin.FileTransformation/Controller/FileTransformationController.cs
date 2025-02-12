using System.Net;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Library;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.FileTransformation.Controller
{
    [Route("[controller]")]
    public class FileTransformationController : ControllerBase
    {
        private readonly IServerApplicationHost m_serverApplicationHost;
        private readonly ILogger<FileTransformationPlugin> m_logger;

        public FileTransformationController(IServerApplicationHost serverApplicationHost, ILogger<FileTransformationPlugin> logger)
        {
            m_serverApplicationHost = serverApplicationHost;
            m_logger = logger;
        }
        
        [HttpPost("RegisterTransformation")]
        public ActionResult RegisterTransformation([FromBody] TransformationRegistrationPayload payload, [FromServices] IWebFileTransformationWriteService writeService)
        {
            writeService.AddTransformation(payload.Id, payload.FileNamePattern, async (path, contents) =>
            {
                await ApplyTransformation(path, contents, payload);
            });
            
            return Ok();
        }

        private async Task ApplyTransformation(string path, Stream contents, TransformationRegistrationPayload payload)
        {
            m_logger.LogInformation($"Transformation requested for '{path}'");
            HttpClient client = new HttpClient();
            if (!(payload.TransformationEndpoint.StartsWith("http") || payload.TransformationEndpoint.StartsWith("https")))
            {
                client.BaseAddress = new Uri(m_serverApplicationHost.GetSmartApiUrl(IPAddress.Loopback));
                m_logger.LogInformation($"Set base address to '{client.BaseAddress}'");
            }
            
            using StreamReader reader = new StreamReader(contents, leaveOpen: true);
            
            JObject obj = new JObject();
            obj.Add("contents", reader.ReadToEnd());
            
            HttpResponseMessage responseMessage = await client
                .PostAsync(payload.TransformationEndpoint, new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json"));
            string transformedString = await responseMessage.Content.ReadAsStringAsync();
            
            m_logger.LogInformation($"Response for request '{responseMessage.RequestMessage?.RequestUri?.ToString()}' received from endpoint '{payload.TransformationEndpoint}' with code {responseMessage.StatusCode} {(int)responseMessage.StatusCode}");
            
            contents.Seek(0, SeekOrigin.Begin);

            using StreamWriter textWriter = new StreamWriter(contents, null, -1, true);
            textWriter.Write(transformedString);
        }
    }
}