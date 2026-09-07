namespace Jellyfin.Plugin.FileTransformation.Library
{
    public interface IWebFileTransformationReadService
    {
        DateTimeOffset GetLastModified(DateTimeOffset? baseLastModified);

        bool NeedsTransformation(string path);

        Task RunTransformation(string path, Stream stream);
    }
}