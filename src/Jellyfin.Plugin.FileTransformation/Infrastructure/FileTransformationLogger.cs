using Jellyfin.Plugin.FileTransformation.Configuration;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class FileTransformationLogger : IFileTransformationLogger
    {
        private ILogger m_logger;
        
        public FileTransformationLogger(ILogger<FileTransformationPlugin> logger)
        {
            m_logger = logger;
        }
        
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return m_logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return m_logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Information)
            {
                if (FileTransformationPlugin.Instance.Configuration.DebugLoggingState == DebugLoggingState.Disabled)
                {
                    return;
                }
            }
            
            m_logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}