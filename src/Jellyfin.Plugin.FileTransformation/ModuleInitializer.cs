using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Jellyfin.Plugin.FileTransformation.Helpers;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation
{
    public class ModuleInitializer
    {
        private static Dictionary<string, Assembly> s_dynamicAssemblies = new Dictionary<string, Assembly>();
        
        public static void Initialize(IApplicationPaths? applicationPaths = null, ILogger? logger = null)
        {
            Assembly assembly = typeof(FileTransformationPlugin).Assembly;
            AssemblyLoadContext assemblyLoadContext = new AssemblyLoadContext("Jellyfin.Plugin.FileTransformation");
            string[] resources = assembly.GetManifestResourceNames();
            
            foreach (string resource in resources.Where(x => x.EndsWith(".dll")))
            {
                logger?.LogInformation($"Loading embedded dll: {Path.GetFileName(resource)}");
                
                using Stream? assemblyStream = assembly.GetManifestResourceStream(resource);
                using MemoryStream memoryStream = new MemoryStream();
                assemblyStream!.CopyTo(memoryStream);
                assemblyStream.Position = 0;
                
                string tmpDllLocation = $"{Path.GetTempFileName()}.dll";

                if (applicationPaths != null)
                {
                    tmpDllLocation = Path.Combine(applicationPaths.TempDirectory, Path.GetFileName(tmpDllLocation));
                }
                
                logger?.LogInformation($"Writing dll to: {tmpDllLocation} to extract AssemblyName details, will be removed after loading");
                File.WriteAllBytes(tmpDllLocation, memoryStream.ToArray());
                
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(tmpDllLocation);
                
                logger?.LogInformation($"Deleting: {tmpDllLocation}");
                File.Delete(tmpDllLocation);
                
                Assembly loadedAssembly;
                if (!assemblyLoadContext.Assemblies.Any(x => x.FullName == assemblyName.FullName))
                {
                    loadedAssembly = assemblyLoadContext.LoadFromStream(assemblyStream);
                }
                else
                {
                    loadedAssembly = assemblyLoadContext.Assemblies.First(x => x.FullName == assemblyName.FullName);
                }
                
                logger?.LogInformation($"Loaded assembly: {loadedAssembly.FullName}");
                s_dynamicAssemblies.Add(loadedAssembly.FullName!, loadedAssembly);
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                if (s_dynamicAssemblies.ContainsKey(args.Name!))
                {
                    return s_dynamicAssemblies[args.Name!];
                }
                
                return null;
            };
            
            PatchHelper.SetupPatches();
        }
    }
}