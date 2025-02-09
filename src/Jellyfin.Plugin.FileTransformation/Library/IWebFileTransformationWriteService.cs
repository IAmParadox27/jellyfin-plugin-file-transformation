namespace Jellyfin.Plugin.FileTransformation.Library
{
    public delegate Task TransformFile(string path, Stream contents);
    
    public interface IWebFileTransformationWriteService
    {
        void AddTransformation(Guid id, string path, TransformFile transformation);
    }
}