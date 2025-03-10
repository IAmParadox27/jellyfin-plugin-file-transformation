using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.FileTransformation.Models
{
    public class TransformationRegistrationPayload
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("fileNamePattern")]
        public string FileNamePattern { get; set; } = string.Empty;
        
        [JsonPropertyName("transformationEndpoint")]
        public string TransformationEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("transformationPipe")]
        public string? TransformationPipe { get; set; } = null;

        [JsonPropertyName("callbackAssembly")]
        public string? CallbackAssembly { get; set; } = null;
        
        [JsonPropertyName("callbackClass")]
        public string? CallbackClass { get; set; } = null;
        
        [JsonPropertyName("callbackMethod")]
        public string? CallbackMethod { get; set; } = null;
    }
}