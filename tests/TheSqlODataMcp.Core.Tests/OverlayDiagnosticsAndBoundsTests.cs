using System.Text;
using TheSqlODataMcp.Core.Catalog;
using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// Regression oracles for the 2026-11-09 independent QA findings SQL7-04 (JSON Pointer escaping in
/// overlay diagnostics) and SQL-A6a (bounded error lists for administrator-authored overlays).
/// </summary>
public sealed class OverlayDiagnosticsAndBoundsTests
{
    [Fact]
    public void SchemaErrorPathsDecodeJsonPointerEscapes()
    {
        const string yaml = """
            catalogVersion: "1.0"
            entities:
              - source: "sales.InvoiceHeader"
                fields:
                  "a/b~c":
                    bogus: true
            """;

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(yaml, string.Empty, EmptyCatalog());

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Code == SemanticOverlayValidationErrorCodes.SchemaViolation
                && error.Path.Contains("a/b~c", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Errors,
            error => error.Path.Contains("a~1b~0c", StringComparison.Ordinal));
    }

    [Fact]
    public void OverlayResultCapsAnEightyThousandErrorListAndKeepsTheTotal()
    {
        var errors = Enumerable.Range(0, 80_000)
            .Select(index => new SemanticOverlayValidationError(
                SemanticOverlayValidationErrorCodes.SchemaViolation,
                $"$.entities[{index}]",
                "Invalid overlay value."))
            .ToArray();

        var result = SemanticOverlayImportResult.Failure(errors);

        Assert.Equal(SemanticOverlayImportResult.MaxReportedErrors, result.Errors.Count);
        Assert.Equal(80_000, result.TotalErrors);
        Assert.True(result.Truncated);
        Assert.Equal("$.entities[0]", result.Errors[0].Path);
        Assert.Equal("$.entities[99]", result.Errors[^1].Path);
    }

    [Fact]
    public void ImporterCapsSchemaErrorsOnAWideInvalidOverlay()
    {
        var builder = new StringBuilder("catalogVersion: \"1.0\"\nentities:\n");
        const int entities = 150;
        for (var index = 0; index < entities; index++)
        {
            builder.Append("  - source: \"sales.InvoiceHeader\"\n")
                .Append("    fields:\n")
                .Append("      InvoiceId:\n")
                .Append("        bogus: true\n");
        }

        var result = SemanticOverlayImporter.ImportYamlAndMarkdown(builder.ToString(), string.Empty, EmptyCatalog());

        Assert.False(result.Succeeded);
        Assert.Equal(SemanticOverlayImportResult.MaxReportedErrors, result.Errors.Count);
        Assert.True(result.TotalErrors > SemanticOverlayImportResult.MaxReportedErrors);
        Assert.True(result.Truncated);
    }

    private static TechnicalCatalog EmptyCatalog() => new("1.0", "sqlserver", Array.Empty<TechnicalEntity>());
}
