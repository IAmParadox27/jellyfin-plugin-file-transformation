using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.FileTransformation.Models
{
    public class TransformationRegistrationPayload
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("fileNamePattern")]
        public string FileNamePattern { get; set; } = string.Empty;
        
        [JsonPropertyName("tranformationEndpoint")]
        public string TransformationEndpoint { get; set; } = string.Empty;
    }
}