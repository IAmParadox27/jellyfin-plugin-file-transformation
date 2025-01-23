using System.Reflection;
using System.Runtime.Loader;
using Emby.Server.Implementations.Plugins;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure
{
    public class FileTransformationAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver m_resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginLoadContext"/> class.
        /// </summary>
        /// <param name="path">The path of the plugin assembly.</param>
        public FileTransformationAssemblyLoadContext(string path) : base(false)
        {
            m_resolver = new AssemblyDependencyResolver(path);
        }

        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = m_resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath is not null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}