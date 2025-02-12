using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class WebFileTransformationService : IWebFileTransformationReadService, IWebFileTransformationWriteService
    {
        private readonly ConcurrentDictionary<string, ICollection<(Guid TransformId, TransformFile Delegate)>> m_fileTransformations = new ConcurrentDictionary<string, ICollection<(Guid TransformId, TransformFile Delegate)>>();
        private readonly ILogger<FileTransformationPlugin> m_logger;

        public WebFileTransformationService(ILogger<FileTransformationPlugin> logger)
        {
            m_logger = logger;
        }
        
        private string NormalizePath(string path)
        {
            return path.TrimStart('/');
        }

        public bool NeedsTransformation(string path)
        {
            if (m_fileTransformations.ContainsKey(NormalizePath(path)))
            {
                return true;
            }

            return m_fileTransformations.Keys.Any(x =>
            {
                Regex r = new Regex(x);

                return r.IsMatch(path);
            });
        }

        public async Task RunTransformation(string path, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            path = NormalizePath(path);

            ICollection<(Guid TransformId, TransformFile Delegate)>? pipeline = null;

            if (m_fileTransformations.ContainsKey(path))
            {
                pipeline = m_fileTransformations[path];
            }
            else
            {
                string? key = m_fileTransformations.Keys.FirstOrDefault(x =>
                {
                    Regex r = new Regex(x);
                    
                    return r.IsMatch(path);
                });

                if (key != null)
                {
                    pipeline = m_fileTransformations[key];
                }
            }

            if (pipeline != null)
            {
                foreach ((_, TransformFile action) in pipeline)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await action(path, stream);
                }
            }
        }

        public void AddTransformation(Guid id, string path, TransformFile transformation)
        {
            m_logger.LogInformation($"Received transformation registration for '{path}' with ID '{id}'");
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (transformation == null)
            {
                m_logger.LogError($"Transformation with ID '{id}' has null callback");
                throw new ArgumentNullException(nameof(transformation));
            }

            path = NormalizePath(path);
            lock (m_fileTransformations)
            {
                if (!m_fileTransformations.TryGetValue(path, out ICollection<(Guid TransformId, TransformFile Delegate)>? pipeline))
                {
                    pipeline = new List<(Guid TransformId, TransformFile Delegate)>();
                    m_fileTransformations[path] = pipeline;
                }

                if (!pipeline.Any(x => x.TransformId == id))
                {
                    pipeline.Add((id, transformation));
                }
            }
        }
    }
}