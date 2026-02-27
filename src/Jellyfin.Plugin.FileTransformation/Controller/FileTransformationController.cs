using System.Net;
using System.Reflection;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Helpers;
using Jellyfin.Plugin.FileTransformation.Library;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller;
using Microsoft.AspNetCore.Authorization;
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

        public FileTransformationController(IServerApplicationHost serverApplicationHost, IFileTransformationLogger logger)
        {
            m_serverApplicationHost = serverApplicationHost;
            m_logger = logger;
        }
        
        [HttpPost("RegisterTransformation")]
        [Authorize(Policy = Policies.RequiresElevation)]
        public ActionResult RegisterTransformation([FromBody] TransformationRegistrationPayload payload, [FromServices] IWebFileTransformationWriteService writeService)
        {
            writeService.AddTransformation(payload.Id, payload.FileNamePattern, async (path, contents) =>
            {
                await TransformationHelper.ApplyTransformation(path, contents, payload, m_logger, m_serverApplicationHost);
            });
            
            return Ok();
        }
    }
}