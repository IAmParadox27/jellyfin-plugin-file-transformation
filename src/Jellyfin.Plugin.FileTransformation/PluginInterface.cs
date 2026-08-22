using System.Collections.Concurrent;
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
        /// <summary>
        /// Registrations received before this plugin's own constructor has run.
        ///
        /// Consumers (Intro Skipper, Home Screen Sections, ...) call RegisterTransformation from
        /// their IHostedService.StartAsync. On Jellyfin 12.0 that can run BEFORE Jellyfin has
        /// constructed FileTransformationPlugin, so Instance is still null. Previously this threw
        /// a NullReferenceException which propagated out of the hosted service and aborted server
        /// startup entirely - one consumer plugin could take the whole server down.
        ///
        /// Early calls are queued here instead and replayed by DrainPending() from the constructor.
        /// </summary>
        private static readonly ConcurrentQueue<JObject> s_pending = new();

        public static void RegisterTransformation(JObject payload)
        {
            FileTransformationPlugin? plugin = FileTransformationPlugin.Instance;

            if (plugin?.ServiceProvider is null)
            {
                s_pending.Enqueue(payload);
                return;
            }

            Register(plugin.ServiceProvider, payload);
        }

        /// <summary>
        /// Replay any registrations that arrived before the plugin was constructed.
        /// Called from FileTransformationPlugin's constructor once ServiceProvider is available.
        /// </summary>
        internal static void DrainPending(IServiceProvider serviceProvider)
        {
            while (s_pending.TryDequeue(out JObject? queued))
            {
                Register(serviceProvider, queued);
            }
        }

        private static void Register(IServiceProvider serviceProvider, JObject payload)
        {
            IWebFileTransformationWriteService writeService =
                serviceProvider.GetRequiredService<IWebFileTransformationWriteService>();

            TransformationRegistrationPayload? castedPayload = payload.ToObject<TransformationRegistrationPayload>();

            if (castedPayload != null)
            {
                // Resolve services eagerly at registration time. The ServiceProvider captured
                // by the plugin is scoped and will be disposed after startup. Resolving lazily
                // inside the callback causes ObjectDisposedException on every subsequent request.
                // Both ILogger and IServerApplicationHost are singletons, so capturing them here is safe.
                ILogger logger = serviceProvider.GetRequiredService<IFileTransformationLogger>();
                IServerApplicationHost serverApplicationHost = serviceProvider.GetRequiredService<IServerApplicationHost>();

                writeService.AddTransformation(castedPayload.Id, castedPayload.FileNamePattern, async (path, contents) =>
                {
                    await TransformationHelper.ApplyTransformation(path, contents, castedPayload, logger, serverApplicationHost);
                });
            }
        }
    }
}
