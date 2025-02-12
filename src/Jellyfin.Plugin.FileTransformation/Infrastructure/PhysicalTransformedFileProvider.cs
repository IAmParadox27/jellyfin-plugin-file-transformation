using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class PhysicalTransformedFileProvider : IFileProvider
    {
        private readonly PhysicalFileProvider m_parentProvider;
        private readonly IWebFileTransformationReadService m_webFileTransformationService;
        private readonly ILogger<FileTransformationPlugin> m_logger;

        public PhysicalTransformedFileProvider(
            PhysicalFileProvider parentProvider,
            IWebFileTransformationReadService webFileTransformationService,
            ILogger<FileTransformationPlugin> logger)
        {
            m_parentProvider = parentProvider;
            m_webFileTransformationService = webFileTransformationService;
            m_logger = logger;
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return m_parentProvider.GetDirectoryContents(subpath);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            IFileInfo iFileInfo = m_parentProvider.GetFileInfo(subpath);
            if (!m_webFileTransformationService.NeedsTransformation(subpath))
            {
                m_logger.LogInformation($"Serving raw file for: {subpath}");
                return iFileInfo;
            }

            m_logger.LogInformation($"Requested file has transformation registered: {subpath}");
            
            if (iFileInfo is PhysicalFileInfo physicalFileInfo)
            {
                MemoryStream transformedStream = new MemoryStream();

                if (physicalFileInfo.Exists)
                {
                    using Stream sourceStream = physicalFileInfo.CreateReadStream();
                    sourceStream.CopyTo(transformedStream);
                    transformedStream.Seek(0, SeekOrigin.Begin);
                }

                m_logger.LogInformation($"Running transformation for: {subpath}");
                m_webFileTransformationService.RunTransformation(subpath, transformedStream).GetAwaiter().GetResult();
                transformedStream.Seek(0, SeekOrigin.Begin);

                return new TransformableFileInfo(physicalFileInfo.Exists ? physicalFileInfo : null, transformedStream, physicalFileInfo.Exists ? null : subpath);
            }
            
            return iFileInfo;
        }

        public IChangeToken Watch(string filter)
        {
            return m_parentProvider.Watch(filter);
        }
    }
}