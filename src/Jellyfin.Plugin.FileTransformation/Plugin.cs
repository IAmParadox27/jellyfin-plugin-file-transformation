using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation;

public class Plugin : BasePlugin<BasePluginConfiguration>
{
    public override Guid Id => Guid.Parse("a5ab19ab-4420-4a5e-8638-76e9a093db95");

    public override string Name => "File Transformation";
    
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IServiceProvider serviceProvider, IHost host, ILogger<Plugin> logger) : base(applicationPaths, xmlSerializer)
    {
    }
}