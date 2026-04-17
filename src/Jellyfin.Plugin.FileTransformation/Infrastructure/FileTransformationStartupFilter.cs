using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public sealed class FileTransformationStartupFilter : IStartupFilter
    {
        private readonly ILogger<FileTransformationStartupFilter> m_logger;

        public FileTransformationStartupFilter(ILogger<FileTransformationStartupFilter> logger)
        {
            m_logger = logger;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                m_logger.LogInformation("[FileTransformation] Installing response transformation middleware");

                app.UseMiddleware<FileTransformationMiddleware>();

                next(app);
            };
        }
    }
}
