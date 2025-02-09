using System.Net;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Library;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.FileTransformation.Controller
{
    [Route("[controller]")]
    public class FileTransformationController : ControllerBase
    {
        private readonly IServerApplicationHost m_serverApplicationHost;

        public FileTransformationController(IServerApplicationHost serverApplicationHost)
        {
            m_serverApplicationHost = serverApplicationHost;
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
            HttpClient client = new HttpClient();
            if (!(payload.TransformationEndpoint.StartsWith("http") || payload.TransformationEndpoint.StartsWith("https")))
            {
                client.BaseAddress = new Uri(m_serverApplicationHost.GetSmartApiUrl(IPAddress.Loopback));
            }
            
            using StreamReader reader = new StreamReader(contents, leaveOpen: true);
            
            JObject obj = new JObject();
            obj.Add("contents", reader.ReadToEnd());
            
            HttpResponseMessage responseMessage = await client
                .PostAsync(payload.TransformationEndpoint, new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json"));
            string transformedString = await responseMessage.Content.ReadAsStringAsync();
            
            contents.Seek(0, SeekOrigin.Begin);

            using StreamWriter textWriter = new StreamWriter(contents, null, -1, true);
            textWriter.Write(transformedString);
        }
    }
}