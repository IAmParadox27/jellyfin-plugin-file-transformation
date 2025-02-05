﻿using System.Text.RegularExpressions;
using Jellyfin.Plugin.FileTransformation.Controller;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class WebFileTransformationService : IWebFileTransformationReadService, IWebFileTransformationWriteService
    {
        private readonly IDictionary<string, ICollection<TransformFile>> m_fileTransformations = new Dictionary<string, ICollection<TransformFile>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebFileTransformationService"/> class.
        /// </summary>
        public WebFileTransformationService()
        {
        }

        private string NormalizePath(string path)
        {
            return path.TrimStart('/');
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void RunTransformation(string path, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            path = NormalizePath(path);

            ICollection<TransformFile>? pipeline = null;

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
                foreach (TransformFile action in pipeline)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    action(path, stream);
                }
            }
        }

        /// <inheritdoc />
        public void AddTransformation(string path, TransformFile transformation)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (transformation == null)
            {
                throw new ArgumentNullException(nameof(transformation));
            }

            path = NormalizePath(path);
            if (!m_fileTransformations.TryGetValue(path, out ICollection<TransformFile>? pipeline))
            {
                pipeline = new List<TransformFile>();
                m_fileTransformations[path] = pipeline;
            }

            pipeline.Add(transformation);
        }
    }
}