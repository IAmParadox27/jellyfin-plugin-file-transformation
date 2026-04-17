using Jellyfin.Plugin.FileTransformation.Infrastructure;
using Jellyfin.Plugin.FileTransformation.Library;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.FileTransformation
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddTransient<IStartupFilter, FileTransformationStartupFilter>();

            // Core transformation service (singleton, registered as both read and write interfaces)
            serviceCollection.AddSingleton<WebFileTransformationService>();
            serviceCollection.AddSingleton<IWebFileTransformationReadService>(s => s.GetRequiredService<WebFileTransformationService>());
            serviceCollection.AddSingleton<IWebFileTransformationWriteService>(s => s.GetRequiredService<WebFileTransformationService>());

            // Logger wrapper
            serviceCollection.AddSingleton<IFileTransformationLogger, FileTransformationLogger>();

            // Config version tracking for auto-refresh
            serviceCollection.AddSingleton<ConfigVersionService>();
        }
    }
}
