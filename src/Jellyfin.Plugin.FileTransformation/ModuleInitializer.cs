using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Jellyfin.Plugin.FileTransformation
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Init()
        {
            Generated.ModuleInitializer.RegisterAssembly("0Harmony, Version=2.3.3.0, Culture=neutral, PublicKeyToken=null", Assembly.GetExecutingAssembly(), "Jellyfin.Plugin.FileTransformation.libs.Harmony.0Harmony.dll");
        }
    }
}