using System.Text.Json.Serialization;

namespace TheSqlODataMCP;

public class AppSettings
{
    [JsonPropertyName("bearerToken")]
    public string BearerToken { get; set; } = string.Empty;

    [JsonPropertyName("sqlConnectionString")]
    public string SqlConnectionString { get; set; } = string.Empty;

    [JsonPropertyName("authSettingsFileName")]
    public string AuthSettingsFileName { get; set; } = "settings.json";
}