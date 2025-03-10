using Jellyfin.Plugin.FileTransformation.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.FileTransformation
{
    public class FileTransformationPlugin : BasePlugin<PluginConfiguration>, IHasPluginConfiguration, IHasWebPages
    {
        public static FileTransformationPlugin Instance { get; private set; } = null!;
        
        public override Guid Id => Guid.Parse("5e87cc92-571a-4d8d-8d98-d2d4147f9f90");

        public override string Name => "File Transformation";
        
        public IServiceProvider ServiceProvider { get; }
        
        public FileTransformationPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IServiceProvider serviceProvider) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            
            ServiceProvider = serviceProvider;
        }
        
        public IEnumerable<PluginPageInfo> GetPages()
        {
            string? prefix = GetType().Namespace;

            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{prefix}.Configuration.config.html"
            };
        }
    }
}