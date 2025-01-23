using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Jellyfin.Plugin.FileTransformation
{
    public static class ModuleInitializer
    {
        public static void TestingPatchPre(ref bool ___isRunning)
        {
            ___isRunning = true;
        }
        
        public static int TestingPatchPost(ref int __result)
        {
            return __result * 2;
        }

        private static Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        
        private static byte[]? LoadAssembly(string path)
        {
            string manifestPath = path.Replace("\\", ".").Replace("/", ".");
            Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(Plugin).Namespace}.{manifestPath}");

            if (stream != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    
                    return ms.ToArray();
                }
            }
            
            return null;
        }
        
        [ModuleInitializer]
        public static void Init()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                Assembly? assembly = AssemblyLoadContext.All.Where(x => !x.IsCollectible).SelectMany(x => x.Assemblies)
                    .FirstOrDefault(x => x.FullName == args.Name);

                if (assembly != null)
                {
                    Console.WriteLine($"Found assembly '{args.Name}' already loaded.");
                    return assembly;
                }
                
                string dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

                if (args.Name.Contains("0Harmony"))
                {
                    Console.WriteLine($"Loading assembly for {args.Name}");
                    //return AppDomain.CurrentDomain.Load(LoadAssembly("libs/Harmony/0Harmony.dll"));
                    return Assembly.LoadFile(Path.Combine(dllPath, "0Harmony.dll"));
                    //return Assembly.Load(LoadAssembly("libs/Harmony/0Harmony.dll") ?? throw new DllNotFoundException("Failed to load Harmony assembly"));
                }
                
                if (args.Name.Contains("Mono.Cecil"))
                {
                    Console.WriteLine($"Loading assembly for {args.Name}");
                    return Assembly.Load(LoadAssembly("libs/Harmony/Mono.Cecil.dll") ?? throw new DllNotFoundException("Failed to load Mono.Cecil assembly"));
                }
                
                if (args.Name.Contains("MonoMod.Utils"))
                {
                    Console.WriteLine($"Loading assembly for {args.Name}");
                    return Assembly.Load(LoadAssembly("libs/Harmony/MonoMod.Utils.dll") ?? throw new DllNotFoundException("Failed to load Mono.Cecil assembly"));
                }
                
                if (args.Name.Contains("MonoMod.Common"))
                {
                    Console.WriteLine($"Loading assembly for {args.Name}");
                    return Assembly.Load(LoadAssembly("libs/Harmony/MonoMod.Common.dll") ?? throw new DllNotFoundException("Failed to load Mono.Cecil assembly"));
                }
                
                Console.WriteLine($"Module Initializer couldn't load assembly: {args.Name}");
                
                return null;
            };
        }
    }
}