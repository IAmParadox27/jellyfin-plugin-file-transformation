namespace Jellyfin.Plugin.FileTransformation.Library
{
    public interface IWebFileTransformationReadService
    {
        bool NeedsTransformation(string path);

        Task RunTransformation(string path, Stream stream);
    }
}