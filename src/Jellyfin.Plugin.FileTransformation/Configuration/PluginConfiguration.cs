using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FileTransformation.Configuration
{
    public enum DebugLoggingState
    {
        Disabled,
        Enabled,
    }
    
    public class PluginConfiguration : BasePluginConfiguration
    {
        public DebugLoggingState DebugLoggingState { get; set; } = DebugLoggingState.Disabled;
    }
}