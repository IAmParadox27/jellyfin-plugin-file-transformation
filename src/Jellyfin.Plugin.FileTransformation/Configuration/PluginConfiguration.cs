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
        
        public PluginDefinedTransformation[] Transformations { get; set; } = Array.Empty<PluginDefinedTransformation>();
    }

    public class PluginDefinedTransformation
    {
        public Guid Id { get; set; }
        
        public string FilenamePattern { get; set; } = string.Empty;
        
        public string SearchText { get; set; } = string.Empty;
        
        public string ReplaceText { get; set; } = string.Empty;
    }
}