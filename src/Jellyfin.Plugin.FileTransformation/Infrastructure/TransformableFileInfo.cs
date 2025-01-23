using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class TransformableFileInfo : IFileInfo
    {
        private readonly PhysicalFileInfo? m_baseInfo;
        private readonly Stream m_transformedStream;
        private readonly string? m_nameOverride;

        public TransformableFileInfo(PhysicalFileInfo? baseInfo, Stream transformedStream, string? nameOverride = null)
        {
            m_baseInfo = baseInfo;
            m_transformedStream = transformedStream;
            m_nameOverride = nameOverride;
        }

        public bool Exists => m_baseInfo?.Exists ?? true; // We assume it exists if we've provided null because we've probably "invented" it.

        public bool IsDirectory => m_baseInfo?.IsDirectory ?? false;

        public DateTimeOffset LastModified => m_baseInfo?.LastModified ?? DateTimeOffset.Now;

        public long Length => m_transformedStream.Length;

        public string Name => m_nameOverride ?? m_baseInfo?.Name ?? string.Empty;

        public string? PhysicalPath => null;

        public Stream CreateReadStream()
        {
            return m_transformedStream;
        }
    }
}