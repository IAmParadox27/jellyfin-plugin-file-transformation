using System.Reflection;
using MediaBrowser.Controller.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Jellyfin.Plugin.FileTransformation.Extensions
{
    public static class JellyfinDllExtensions
    {
        private static Type? GetExtensionType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).FirstOrDefault(type => type.Name == name);
        }
        
        public static void UseBaseUrlRedirection(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseBaseUrlRedirection),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UsePathTrim(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UsePathTrim),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseRobotsRedirection(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseRobotsRedirection),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseJellyfinApiSwagger(this IApplicationBuilder app, IServerConfigurationManager serverConfigurationManager)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseJellyfinApiSwagger), 
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app, serverConfigurationManager });
        }

        public static void UseQueryStringDecoding(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseQueryStringDecoding), 
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseLanFiltering(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseLanFiltering),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseIPBasedAccessValidation(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseIPBasedAccessValidation),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseWebSocketHandler(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseWebSocketHandler),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }

        public static void UseServerStartupMessage(this IApplicationBuilder app)
        {
            MethodInfo? method = GetExtensionType("ApiApplicationBuilderExtensions")?.GetMethod(nameof(UseServerStartupMessage),
                BindingFlags.Static | BindingFlags.Public);
            
            method?.Invoke(null, new object[] { app });
        }
    }
}