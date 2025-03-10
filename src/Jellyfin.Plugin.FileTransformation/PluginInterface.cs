using Jellyfin.Plugin.FileTransformation.Helpers;
using Jellyfin.Plugin.FileTransformation.Library;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.FileTransformation
{
    public static class PluginInterface
    {
        public static void RegisterTransformation(JObject payload)
        {
            IWebFileTransformationWriteService writeService = FileTransformationPlugin.Instance.ServiceProvider
                .GetRequiredService<IWebFileTransformationWriteService>();

            TransformationRegistrationPayload? castedPayload = payload.ToObject<TransformationRegistrationPayload>();

            if (castedPayload != null)
            {
                writeService.AddTransformation(castedPayload.Id, castedPayload.FileNamePattern, async (path, contents) =>
                {
                    ILogger logger = FileTransformationPlugin.Instance.ServiceProvider.GetRequiredService<IFileTransformationLogger>();
                    IServerApplicationHost serverApplicationHost = FileTransformationPlugin.Instance.ServiceProvider.GetRequiredService<IServerApplicationHost>();
                    
                    await TransformationHelper.ApplyTransformation(path, contents, castedPayload, logger, serverApplicationHost);
                });
            }
        }
    }
}