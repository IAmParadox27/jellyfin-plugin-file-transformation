using Jellyfin.Plugin.FileTransformation.Controller;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    /// <summary>
    /// Provides file contents modified by <see cref="IWebFileTransformationReadService"/>.
    /// </summary>
    public class PhysicalTransformedFileProvider : IFileProvider
    {
        private readonly PhysicalFileProvider m_parentProvider;
        private readonly IWebFileTransformationReadService m_webFileTransformationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalTransformedFileProvider"/> class based on the set parent provider.
        /// </summary>
        /// <param name="parentProvider">The parent provider.</param>
        /// <param name="webFileTransformationService">The <see cref="IWebFileTransformationReadService"/>.</param>
        public PhysicalTransformedFileProvider(
            PhysicalFileProvider parentProvider,
            IWebFileTransformationReadService webFileTransformationService)
        {
            m_parentProvider = parentProvider;
            m_webFileTransformationService = webFileTransformationService;
        }

        /// <inheritdoc />
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return m_parentProvider.GetDirectoryContents(subpath);
        }

        /// <inheritdoc />
        public IFileInfo GetFileInfo(string subpath)
        {
            IFileInfo iFileInfo = m_parentProvider.GetFileInfo(subpath);
            if (!m_webFileTransformationService.NeedsTransformation(subpath))
            {
                return iFileInfo;
            }

            if (iFileInfo is PhysicalFileInfo physicalFileInfo)
            {
                MemoryStream transformedStream = new MemoryStream();

                if (physicalFileInfo.Exists)
                {
                    using Stream sourceStream = physicalFileInfo.CreateReadStream();
                    sourceStream.CopyTo(transformedStream);
                    transformedStream.Seek(0, SeekOrigin.Begin);
                }

                m_webFileTransformationService.RunTransformation(subpath, transformedStream);
                transformedStream.Seek(0, SeekOrigin.Begin);

                return new TransformableFileInfo(physicalFileInfo.Exists ? physicalFileInfo : null, transformedStream, physicalFileInfo.Exists ? null : subpath);
            }
            
            return iFileInfo;
        }

        /// <inheritdoc />
        public IChangeToken Watch(string filter)
        {
            return m_parentProvider.Watch(filter);
        }
    }
}