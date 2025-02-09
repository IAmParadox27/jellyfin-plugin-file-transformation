using System.Diagnostics.CodeAnalysis;
using System.Reflection;

[assembly: SuppressMessage("Intentional", "CA2255", Justification = "In order for Jellyfin plugins to be referenceable by other plugins they must be initialized in a non-collectable AssemblyLoadContext. This dll is designed to be a starting point for those types of plugins to avoid individual plugin devs from needing to do this.")]

[assembly: AssemblyCompany("IAmParadox27")]
[assembly: AssemblyProduct("Jellyfin.Plugin.FileTransformation")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyTitle("Jellyfin.Plugin.FileTransformation")]
[assembly: AssemblyVersion("1.2.0.0")]