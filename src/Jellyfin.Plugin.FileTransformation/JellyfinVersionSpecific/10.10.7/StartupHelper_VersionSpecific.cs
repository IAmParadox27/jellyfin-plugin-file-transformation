using Microsoft.AspNetCore.Builder;

namespace Jellyfin.Plugin.FileTransformation.JellyfinVersionSpecific
{
    public static class StartupHelper_VersionSpecific
    {
        public static StaticFileOptions ConfigureVersionSpecific(this StaticFileOptions options)
        {
            return options;
        }
    }
}