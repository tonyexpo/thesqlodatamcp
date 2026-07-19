using System.Text.Json.Nodes;
using Json.Schema;
using Markdig;
using Markdig.Extensions.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Xunit;

namespace TheSqlODataMcp.Spikes.CatalogValidation;

public sealed class CatalogValidationTests
{
    [Fact]
    public void Json_schema_rejects_an_unknown_property_when_additional_properties_is_false()
    {
        var schema = JsonSchema.FromText("""
            {"type":"object","properties":{"title":{"type":"string"}},"required":["title"],"additionalProperties":false}
            """);

        var result = schema.Evaluate(JsonNode.Parse("{" + "\"title\":\"Sales\",\"typo\":true}"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Yaml_deserializer_rejects_an_unknown_property()
    {
        // Strict rejection is YamlDotNet's default. Do not call IgnoreUnmatchedProperties().
        var deserializer = CreateDeserializer();

        Assert.Throws<YamlException>(() =>
        {
            _ = deserializer.Deserialize<CatalogHeader>("title: Sales\ntypo: true\n");
        });
    }

    [Fact]
    public void Markdown_yaml_front_matter_is_extracted_and_deserialized()
    {
        var document = Markdown.Parse("""
            ---
            title: Sales
            ---
            # Sales reporting guide
            """, new MarkdownPipelineBuilder().UseYamlFrontMatter().Build());
        var frontMatter = Assert.Single(document.OfType<YamlFrontMatterBlock>());
        var deserializer = CreateDeserializer();

        var header = deserializer.Deserialize<CatalogHeader>(frontMatter.Lines.ToString());

        Assert.Equal("Sales", header.Title);
    }

    [Fact]
    public void Markdown_yaml_front_matter_is_extracted_and_rejected_when_a_typed_key_is_unknown()
    {
        var document = Markdown.Parse("""
            ---
            title: Sales
            typo: true
            ---
            # Sales reporting guide
            """, new MarkdownPipelineBuilder().UseYamlFrontMatter().Build());
        var frontMatter = Assert.Single(document.OfType<YamlFrontMatterBlock>());
        var deserializer = CreateDeserializer();

        Assert.Throws<YamlException>(() =>
        {
            _ = deserializer.Deserialize<CatalogHeader>(frontMatter.Lines.ToString());
        });
    }

    private sealed class CatalogHeader
    {
        public string? Title { get; init; }
    }

    private static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
}
