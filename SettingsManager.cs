using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheSqlODataMCP;

public class SettingsManager
{
    private readonly string _settingsFilePath;
    private AppSettings? _settings;

    public SettingsManager(string settingsFilePath = "settings.json")
    {
        _settingsFilePath = settingsFilePath;
        LoadSettings();
    }

    public void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            throw new FileNotFoundException($"Settings file not found: {_settingsFilePath}");
        }

        string jsonContent = File.ReadAllText(_settingsFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        try
        {
            _settings = JsonSerializer.Deserialize<AppSettings>(jsonContent, options);
            if (_settings == null)
            {
                throw new InvalidOperationException("Failed to deserialize settings.");
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Failed to deserialize settings.");
        }
    }

    public AppSettings GetSettings() => _settings!;
    
    public string GetBearerToken() => _settings!.BearerToken;
    
    public string GetSqlConnectionString() => _settings!.SqlConnectionString;
}