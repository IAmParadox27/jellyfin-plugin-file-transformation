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
                
                string? tmpDllLocation = $"{Path.GetTempFileName()}.dll";
                AssemblyName? assemblyName = null;
                
                if (applicationPaths != null)
                {
                    tmpDllLocation = Path.Combine(applicationPaths.TempDirectory, Path.GetFileName(tmpDllLocation));
                    
                    FileInfo dllFileInfo = new FileInfo(tmpDllLocation);

                    try
                    {
                        dllFileInfo.Directory?.Create();
                    }
                    catch
                    {
                        // We tried the supplied jellyfin TempDirectory and we didn't have write permissions. We're going to just use the configurations directory
                        // as if this fails the user has other problems.
                        logger?.LogWarning($"Unable to create temp directory for file transformation plugin. Tried '{applicationPaths.TempDirectory}'. Now attempting '{applicationPaths.ConfigurationDirectoryPath}' before failing.");

                        try
                        {
                            tmpDllLocation = Path.Combine(applicationPaths.ConfigurationDirectoryPath, Path.GetFileName(tmpDllLocation));
                            dllFileInfo = new FileInfo(tmpDllLocation);
                            dllFileInfo.Directory?.Create();
                        }
                        catch
                        {
                            logger?.LogError($"Unable to create temp directory for file transformation plugin. Tried '{applicationPaths.TempDirectory}' and '{applicationPaths.ConfigurationDirectoryPath}' as directories, neither allowed writing. Falling back to using a fallback AssemblyLoadContext and loading DLL to get assembly name before unloading and loading properly.");
                            tmpDllLocation = null;
                        }
                    }
                }

                try
                {
                    if (tmpDllLocation == null)
                    {
                        // We're expecting this when we can't create the directories. Lets just send an exception to enter the catch
                        throw new FileNotFoundException($"Null DLL location.");
                    }
                    
                    logger?.LogInformation($"Writing dll to: {tmpDllLocation} to extract AssemblyName details, will be removed after loading");
                    File.WriteAllBytes(tmpDllLocation, memoryStream.ToArray());

                    assemblyName = AssemblyName.GetAssemblyName(tmpDllLocation);

                    logger?.LogInformation($"Deleting: {tmpDllLocation}");
                    File.Delete(tmpDllLocation);
                }
                catch
                {
                    // Attempting final fallback
                    AssemblyLoadContext throwAwayContext = new AssemblyLoadContext($"{typeof(ModuleInitializer).Namespace}.FallbackContext", true);
                    Assembly tmpLoadedAssembly = throwAwayContext.LoadFromStream(assemblyStream);
                    assemblyStream.Position = 0;

                    assemblyName = tmpLoadedAssembly.GetName();
                            
                    logger?.LogInformation($"Retrieved AssemblyName from fallback context '{throwAwayContext.Name}'. Now unloading");
                    throwAwayContext.Unload();
                }

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