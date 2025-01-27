using System.Net.Mime;
using System.Reflection;
using System.Runtime.Loader;
using Emby.Server.Implementations.Plugins;
using HarmonyLib;
using Jellyfin.Api.Middleware;
using Jellyfin.Plugin.FileTransformation.Controller;
using Jellyfin.Plugin.FileTransformation.Infrastructure;
using Jellyfin.Plugin.Referenceable.Services;
using Jellyfin.Server;
using Jellyfin.Server.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Extensions;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Jellyfin.Plugin.FileTransformation
{
    public class FileTransformPluginServiceRegistrator : PluginServiceRegistrator
    {
        public override void ConfigureServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            Harmony harmony = new Harmony("dev.iamparadox.jellyfin")!;
            
            HarmonyMethod configureStartupPatchMethod = new HarmonyMethod(typeof(FileTransformPluginServiceRegistrator).GetMethod(nameof(Configure_StartupPatch)));
            
            harmony.Patch(typeof(Startup).GetMethod(nameof(Startup.Configure)),
                prefix: configureStartupPatchMethod);
            
            serviceCollection.AddSingleton<WebFileTransformationService>()
                .AddSingleton<IWebFileTransformationReadService>(s => s.GetRequiredService<WebFileTransformationService>())
                .AddSingleton<IWebFileTransformationWriteService>(s => s.GetRequiredService<WebFileTransformationService>());
        }

        public static bool Configure_StartupPatch(IApplicationBuilder app,
            IWebHostEnvironment env,
            IConfiguration appConfig,
            ref object __instance)
        {
            FieldInfo? serverConfigurationManagerInfo = __instance.GetType().GetField("_serverConfigurationManager", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new ArgumentNullException("__instance.GetType().GetField(\"_serverConfigurationManager\", BindingFlags.Instance | BindingFlags.NonPublic)");
            FieldInfo? serverApplicationHostInfo = __instance.GetType().GetField("_serverApplicationHost", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new ArgumentNullException("__instance.GetType().GetField(\"_serverApplicationHost\", BindingFlags.Instance | BindingFlags.NonPublic)");

            IServerConfigurationManager? serverConfigurationManager = serverConfigurationManagerInfo?.GetValue(__instance) as IServerConfigurationManager;
            IServerApplicationHost? serverApplicationHost = serverApplicationHostInfo?.GetValue(__instance) as IServerApplicationHost;

            app.UseBaseUrlRedirection();

            // Wrap rest of configuration so everything only listens on BaseUrl.
            var config = serverConfigurationManager.GetNetworkConfiguration();
            app.Map(config.BaseUrl, mainApp =>
            {
                if (env.IsDevelopment())
                {
                    mainApp.UseDeveloperExceptionPage();
                }

                mainApp.UseForwardedHeaders();
                mainApp.UseMiddleware<ExceptionMiddleware>();

                mainApp.UseMiddleware<ResponseTimeMiddleware>();

                mainApp.UseWebSockets();

                mainApp.UseResponseCompression();

                mainApp.UseCors();

                if (config.RequireHttps && serverApplicationHost.ListenWithHttps)
                {
                    mainApp.UseHttpsRedirection();
                }

                // This must be injected before any path related middleware.
                mainApp.UsePathTrim();
                
                if (appConfig.HostWebClient())
                {
                    var extensionProvider = new FileExtensionContentTypeProvider();

                    // subtitles octopus requires .data, .mem files.
                    extensionProvider.Mappings.Add(".data", MediaTypeNames.Application.Octet);
                    extensionProvider.Mappings.Add(".mem", MediaTypeNames.Application.Octet);
                    mainApp.UseDefaultFiles(new DefaultFilesOptions
                    {
                        FileProvider = new PhysicalTransformedFileProvider(
                            new PhysicalFileProvider(serverConfigurationManager.ApplicationPaths.WebPath),
                            mainApp.ApplicationServices.GetRequiredService<IWebFileTransformationReadService>()),
                        RequestPath = "/web"
                    });
                    mainApp.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalTransformedFileProvider(
                            new PhysicalFileProvider(serverConfigurationManager.ApplicationPaths.WebPath),
                            mainApp.ApplicationServices.GetRequiredService<IWebFileTransformationReadService>()),
                        RequestPath = "/web",
                        ContentTypeProvider = extensionProvider
                    });

                    mainApp.UseRobotsRedirection();
                }

                mainApp.UseStaticFiles();
                mainApp.UseAuthentication();
                mainApp.UseJellyfinApiSwagger(serverConfigurationManager);
                mainApp.UseQueryStringDecoding();
                mainApp.UseRouting();
                mainApp.UseAuthorization();

                mainApp.UseLanFiltering();
                mainApp.UseIPBasedAccessValidation();
                mainApp.UseWebSocketHandler();
                mainApp.UseServerStartupMessage();

                if (serverConfigurationManager.Configuration.EnableMetrics)
                {
                    // Must be registered after any middleware that could change HTTP response codes or the data will be bad
                    mainApp.UseHttpMetrics();
                }

                mainApp.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    if (serverConfigurationManager.Configuration.EnableMetrics)
                    {
                        endpoints.MapMetrics();
                    }

                    endpoints.MapHealthChecks("/health");
                });
            });

            return false;
        }
        
    }
}