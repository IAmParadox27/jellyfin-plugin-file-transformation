using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.FileTransformation
{
    public class FileTransformationPlugin : BasePlugin<BasePluginConfiguration>
    {
        public override Guid Id => Guid.Parse("5e87cc92-571a-4d8d-8d98-d2d4147f9f90");

        public override string Name => "File Transformation";
        
        public FileTransformationPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
        }
    }
}