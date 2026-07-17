using System.IO;
using Xunit;

namespace TheSqlODataMCP.Tests;

public class SettingsManagerTests
{
    private readonly string _testSettingsFile = "test_settings.json";

    [Fact]
    public void LoadSettings_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        File.WriteAllText(_testSettingsFile, @"{
            ""bearerToken"": ""test_token"",
            ""sqlConnectionString"": ""Server=localhost;Database=master;TrustCertificate=True;"",
            ""authSettingsFileName"": ""settings.json""
        }");

        // Act
        var settingsManager = new SettingsManager(_testSettingsFile);

        // Assert
        Assert.NotNull(settingsManager.GetSettings());
        Assert.Equal("test_token", settingsManager.GetBearerToken());
        Assert.Equal("Server=localhost;Database=master;TrustCertificate=True;", settingsManager.GetSqlConnectionString());
        
        // Cleanup
        File.Delete(_testSettingsFile);
    }

    [Fact]
    public void LoadSettings_MissingFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() => new SettingsManager("non_existent_settings.json"));
        Assert.Contains("Settings file not found: non_existent_settings.json", exception.Message);
    }

    [Fact]
    public void LoadSettings_InvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        File.WriteAllText(_testSettingsFile, "invalid json content");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new SettingsManager(_testSettingsFile));
        Assert.Contains("Failed to deserialize settings.", exception.Message);
        
        // Cleanup
        File.Delete(_testSettingsFile);
    }
}