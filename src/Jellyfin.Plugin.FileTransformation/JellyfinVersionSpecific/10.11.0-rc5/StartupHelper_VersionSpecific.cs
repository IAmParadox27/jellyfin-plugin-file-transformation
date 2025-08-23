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
                if (Path.GetFileName(context.File.Name).Equals("index.html", StringComparison.Ordinal))
                {
                    context.Context.Response.Headers.CacheControl = new StringValues("no-cache");
                }
            };
            
            return options;
        }
    }
}