using System.Reflection;
using HarmonyLib;

namespace Jellyfin.Plugin.FileTransformation.Helpers
{
    public static class PatchHelper
    {
        private static Harmony s_harmony = new Harmony("dev.iamparadox.jellyfin");

        internal static void SetupPatches()
        {
            HarmonyMethod configureStartupPatchMethod = new HarmonyMethod(typeof(StartupHelper).GetMethod(nameof(StartupHelper.Patch_Startup_Configure), BindingFlags.NonPublic | BindingFlags.Static));

            // We patch the Startup.Configure function to allow things to be changed while the app is being setup.
            // Currently the only configurable element is the FileProvider for Default/Static files for /web but 
            // as there are more requirements this will update to include those too.
            Type startupType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).FirstOrDefault(x => x.Name == "Startup")!;
            s_harmony.Patch(startupType.GetMethod("Configure"),
                prefix: configureStartupPatchMethod);
        }
    }
}