using Jellyfin.Plugin.FileTransformation.Library;
using Jellyfin.Plugin.FileTransformation.Infrastructure;
using Jellyfin.Plugin.FileTransformation.Helpers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            StartupHelper.WebDefaultFilesFileProvider = GetFileTransformationFileProvider;
            StartupHelper.WebStaticFilesFileProvider = GetFileTransformationFileProvider;
            
            serviceCollection.AddSingleton<WebFileTransformationService>()
                .AddSingleton<IWebFileTransformationReadService>(s => s.GetRequiredService<WebFileTransformationService>())
                .AddSingleton<IWebFileTransformationWriteService>(s => s.GetRequiredService<WebFileTransformationService>());

            serviceCollection.AddSingleton<IFileTransformationLogger, FileTransformationLogger>();

            serviceCollection.AddHostedService<NamedPipeService>();
        }

        private IFileProvider GetFileTransformationFileProvider(IServerConfigurationManager serverConfigurationManager, IApplicationBuilder mainApplicationBuilder)
        {
            return new PhysicalTransformedFileProvider(
                new PhysicalFileProvider(serverConfigurationManager.ApplicationPaths.WebPath),
                mainApplicationBuilder.ApplicationServices.GetRequiredService<IWebFileTransformationReadService>(),
                mainApplicationBuilder.ApplicationServices.GetRequiredService<IFileTransformationLogger>());
        }
    }
}