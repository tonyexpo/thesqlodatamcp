using System.Text.Json;
using System.Xml.Linq;
using TheSqlODataMcp.Web;
using Xunit;

namespace TheSqlODataMcp.IntegrationTests;

public sealed class ScaffoldConfigurationTests
{
    [Fact]
    public void WebComposesEachProductionBoundaryOnce()
    {
        Assert.Equal(
            ["Core", "SqlServer", "Persistence", "Protocols"],
            WebAssemblyMarker.ComposedBoundaryNames);
    }

    [Fact]
    public void ConfigurationUsesTheHandoffSectionShapesWithBlankSensitiveValues()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var configuration = document.RootElement;

        foreach (var section in new[] { "DataSource", "ControlStore", "Catalog", "Query", "OAuth", "Admin", "Hosting", "Features" })
        {
            Assert.True(configuration.TryGetProperty(section, out _), $"Missing {section} configuration section.");
        }

        Assert.Equal("SqlServer", configuration.GetProperty("DataSource").GetProperty("Provider").GetString());
        Assert.Equal(string.Empty, configuration.GetProperty("DataSource").GetProperty("ConnectionString").GetString());
        Assert.Equal("Sqlite", configuration.GetProperty("ControlStore").GetProperty("Provider").GetString());
        Assert.Equal(string.Empty, configuration.GetProperty("ControlStore").GetProperty("ConnectionString").GetString());
        Assert.Equal("ImportIfChanged", configuration.GetProperty("Catalog").GetProperty("BootstrapMode").GetString());
        Assert.Equal("Strict", configuration.GetProperty("Catalog").GetProperty("ValidationMode").GetString());
        Assert.Equal(10000, configuration.GetProperty("Query").GetProperty("MaxRows").GetInt32());
        Assert.Equal(100, configuration.GetProperty("OAuth").GetProperty("DynamicRegistrationLimit").GetInt32());
        Assert.Equal(string.Empty, configuration.GetProperty("Admin").GetProperty("BootstrapToken").GetString());
        Assert.Equal(string.Empty, configuration.GetProperty("Hosting").GetProperty("PublicBaseUrl").GetString());
        Assert.True(configuration.GetProperty("Hosting").GetProperty("TrustForwardedHeaders").GetBoolean());
    }

    [Fact]
    public void PackageVersionsAndPlacementsMatchTheAcceptedBaselines()
    {
        var packageVersions = ReadPackageVersions("Directory.Packages.props");

        Assert.Equal("7.3.0", packageVersions["JsonSchema.Net"]);
        Assert.Equal("0.42.0", packageVersions["Markdig"]);
        Assert.Equal("9.5.0", packageVersions["Microsoft.AspNetCore.OData"]);
        Assert.Equal("6.1.1", packageVersions["Microsoft.Data.SqlClient"]);
        Assert.Equal("1.4.1", packageVersions["ModelContextProtocol.AspNetCore"]);
        Assert.Equal("7.6.0", packageVersions["OpenIddict.Server.AspNetCore"]);
        Assert.Equal("16.3.0", packageVersions["YamlDotNet"]);
        Assert.Equal("17.14.1", packageVersions["Microsoft.NET.Test.Sdk"]);
        Assert.Equal("2.9.3", packageVersions["xunit"]);
        Assert.Equal("3.1.4", packageVersions["xunit.runner.visualstudio"]);

        Assert.Equal(["JsonSchema.Net", "Markdig", "YamlDotNet"], ReadPackageReferenceNames("TheSqlODataMcp.Core.csproj").Order());
        Assert.Empty(ReadPackageReferenceNames("TheSqlODataMcp.Persistence.csproj"));
        Assert.Equal(
            ["Microsoft.AspNetCore.OData", "ModelContextProtocol.AspNetCore"],
            ReadPackageReferenceNames("TheSqlODataMcp.Protocols.csproj").Order());
        Assert.Equal(["Microsoft.Data.SqlClient"], ReadPackageReferenceNames("TheSqlODataMcp.SqlServer.csproj").Order());
        Assert.Equal(["OpenIddict.Server.AspNetCore"], ReadPackageReferenceNames("TheSqlODataMcp.Web.csproj").Order());
    }

    private static Dictionary<string, string> ReadPackageVersions(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "scaffold", fileName);
        var document = XDocument.Load(path);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .ToDictionary(
                element => element.Attribute("Include")?.Value ?? throw new InvalidOperationException("Package name is required."),
                element => element.Attribute("Version")?.Value ?? throw new InvalidOperationException("Package version is required."),
                StringComparer.Ordinal);
    }

    private static IEnumerable<string> ReadPackageReferenceNames(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "scaffold", fileName);
        var document = XDocument.Load(path);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(
                element =>
                {
                    Assert.Null(element.Attribute("Version"));
                    return element.Attribute("Include")?.Value ?? throw new InvalidOperationException("Package name is required.");
                });
    }
}
