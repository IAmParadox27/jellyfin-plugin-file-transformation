using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.FileTransformation.JellyfinVersionSpecific
{
    public static class StartupHelper_VersionSpecific
    {
        public static StaticFileOptions ConfigureVersionSpecific(this StaticFileOptions options)
        {
            options.OnPrepareResponse = (context) =>
            {
                var fileName = Path.GetFileName(context.File.Name);
                if (fileName.Equals("index.html", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("main.jellyfin.bundle.js", StringComparison.OrdinalIgnoreCase))
                {
                    context.Context.Response.Headers.CacheControl = new StringValues("no-cache");
                }
            };

            return options;
        }
    }
}