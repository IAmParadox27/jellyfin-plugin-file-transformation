using System.Collections.Concurrent;
using System.Security.Cryptography;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public sealed class ConfigVersionService : IDisposable
    {
        private readonly ILogger<ConfigVersionService> m_logger;
        private readonly FileSystemWatcher? m_watcher;
        private readonly ConcurrentDictionary<string, string> m_fileHashes = new ConcurrentDictionary<string, string>();
        private long m_version;

        public ConfigVersionService(ILogger<ConfigVersionService> logger, IApplicationPaths appPaths)
        {
            m_logger = logger;
            m_version = Random.Shared.NextInt64(1000000, 9999999);

            string? configPath = appPaths.PluginConfigurationsPath;
            if (string.IsNullOrEmpty(configPath) || !Directory.Exists(configPath))
            {
                m_logger.LogWarning($"[FileTransformation] Config path '{configPath ?? "(null)"}' not found, auto-refresh disabled");
                return;
            }

            // Snapshot existing file hashes so the first real change detects a diff
            // rather than counting the first save after startup as a change.
            foreach (string file in Directory.EnumerateFiles(configPath, "*.xml", SearchOption.TopDirectoryOnly))
            {
                string? hash = TryComputeFileHash(file);
                if (hash != null)
                {
                    m_fileHashes[file] = hash;
                }
            }

            m_watcher = new FileSystemWatcher(configPath, "*.xml")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
            };

            m_watcher.Changed += OnConfigChanged;
            m_watcher.Created += OnConfigChanged;
            m_watcher.Error += OnWatcherError;

            m_logger.LogInformation($"[FileTransformation] Config version watcher active on: {configPath}");
        }

        public long Version => Interlocked.Read(ref m_version);

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Plugin.UpdateConfiguration writes unconditionally on every Save click, even
            // when no fields changed, and every logical save fires 1-3 Changed events. Hashing
            // the file contents skips both cases so open browsers aren't refreshed for no reason.
            string? hash = TryComputeFileHash(e.FullPath);
            if (hash == null)
            {
                return;
            }

            if (m_fileHashes.TryGetValue(e.FullPath, out string? previousHash) && previousHash == hash)
            {
                return;
            }

            m_fileHashes[e.FullPath] = hash;

            long newVersion = Interlocked.Increment(ref m_version);
            m_logger.LogInformation($"[FileTransformation] Config change detected ({e.Name}), version now {newVersion}");
        }

        private string? TryComputeFileHash(string path)
        {
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] hashBytes = SHA256.HashData(stream);
                return Convert.ToHexString(hashBytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // File may be mid-write or deleted between event firing and read.
                // The next Changed event for this path will catch up.
                return null;
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            m_logger.LogError(e.GetException(), "[FileTransformation] FileSystemWatcher error — config change detection may be unreliable");
        }

        public void Dispose()
        {
            m_watcher?.Dispose();
        }
    }
}
