using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class WebFileTransformationService : IWebFileTransformationReadService, IWebFileTransformationWriteService
    {
        private static readonly TimeSpan s_regexTimeout = TimeSpan.FromSeconds(2);

        private readonly ConcurrentDictionary<string, ICollection<(Guid TransformId, TransformFile Delegate)>> m_fileTransformations = new ConcurrentDictionary<string, ICollection<(Guid TransformId, TransformFile Delegate)>>();
        private readonly ConcurrentDictionary<string, Regex> m_regexCache = new ConcurrentDictionary<string, Regex>();
        private readonly object m_pipelineLock = new object();
        private readonly ILogger<FileTransformationPlugin> m_logger;

        public WebFileTransformationService(IFileTransformationLogger logger)
        {
            m_logger = logger;
        }

        private string NormalizePath(string path)
        {
            return path.TrimStart('/');
        }

        private Regex GetOrCreateRegex(string pattern)
        {
            return m_regexCache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled, s_regexTimeout));
        }

        public bool NeedsTransformation(string path)
        {
            path = NormalizePath(path);

            if (m_fileTransformations.ContainsKey(path))
            {
                return true;
            }

            return m_fileTransformations.Keys.Any(key =>
            {
                try
                {
                    return GetOrCreateRegex(key).IsMatch(path);
                }
                catch (RegexMatchTimeoutException)
                {
                    m_logger.LogWarning($"[FileTransformation] Regex timeout matching pattern '{key}' against '{path}'");
                    return false;
                }
                catch (ArgumentException ex)
                {
                    m_logger.LogWarning(ex, $"[FileTransformation] Invalid regex pattern '{key}'");
                    return false;
                }
            });
        }

        public async Task RunTransformation(string path, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            path = NormalizePath(path);

            // Find the matching pipeline — exact match first, then regex
            ICollection<(Guid TransformId, TransformFile Delegate)>? pipeline = null;

            if (m_fileTransformations.TryGetValue(path, out ICollection<(Guid TransformId, TransformFile Delegate)>? exactMatch))
            {
                pipeline = exactMatch;
            }
            else
            {
                string? key = m_fileTransformations.Keys.FirstOrDefault(k =>
                {
                    try
                    {
                        return GetOrCreateRegex(k).IsMatch(path);
                    }
                    catch (Exception ex) when (ex is RegexMatchTimeoutException or ArgumentException)
                    {
                        m_logger.LogWarning(ex, $"[FileTransformation] Regex error for pattern '{k}'");
                        return false;
                    }
                });

                if (key != null)
                {
                    m_fileTransformations.TryGetValue(key, out pipeline);
                }
            }

            if (pipeline == null)
            {
                return;
            }

            // Snapshot under lock to avoid races with Add/Remove
            List<(Guid TransformId, TransformFile Delegate)> transforms;
            lock (m_pipelineLock)
            {
                transforms = pipeline.ToList();
            }

            foreach ((Guid transformId, TransformFile action) in transforms)
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await action(path, stream);
                }
                catch (Exception ex)
                {
                    m_logger.LogError(ex, $"[FileTransformation] Transform {transformId} failed for '{path}'. Continuing with next.");
                }
            }

            stream.Seek(0, SeekOrigin.Begin);
        }

        public void AddTransformation(Guid id, string path, TransformFile transformation)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(transformation);

            path = NormalizePath(path);
            m_logger.LogInformation($"[FileTransformation] Registering transformation for '{path}' (ID: {id})");

            lock (m_pipelineLock)
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

        public void RemoveTransformation(Guid id)
        {
            lock (m_pipelineLock)
            {
                List<string> emptyKeys = new List<string>();

                foreach (KeyValuePair<string, ICollection<(Guid TransformId, TransformFile Delegate)>> pipelines in m_fileTransformations)
                {
                    (Guid TransformId, TransformFile Delegate) match = pipelines.Value.FirstOrDefault(x => x.TransformId == id);
                    if (match != default)
                    {
                        pipelines.Value.Remove(match);
                        if (pipelines.Value.Count == 0)
                        {
                            emptyKeys.Add(pipelines.Key);
                        }
                    }
                }

                // Clean up empty pipelines and stale regex cache entries
                foreach (string key in emptyKeys)
                {
                    m_fileTransformations.TryRemove(key, out _);
                    m_regexCache.TryRemove(key, out _);
                }
            }
        }

        public void UpdateTransformation(Guid id, string path, TransformFile transformation)
        {
            RemoveTransformation(id);
            AddTransformation(id, path, transformation);
        }
    }
}
