# Catalog YAML/front-matter and JSON Schema spike

## Selection and result

- YAML/front matter parser: `YamlDotNet` **16.3.0**.
- Markdown/front-matter extractor: `Markdig` **0.42.0**.
- JSON Schema evaluator: `JsonSchema.Net` **7.3.0** (`Json.Schema` namespace).

The tests show an end-to-end Markdown front-matter extraction path and two independent strictness controls required by the catalog importer:

1. `Markdig` with `UseYamlFrontMatter()` extracts the opening YAML front-matter block from a Markdown guide; its source is passed to the typed `YamlDotNet` deserializer.
2. `YamlDotNet`, configured with `CamelCaseNamingConvention`, accepts the catalog's camel-case keys and rejects unknown YAML mapping keys while deserializing the typed front-matter DTO. The importer must not call its opt-in `IgnoreUnmatchedProperties()` method.
3. A schema with `additionalProperties: false` causes `JsonSchema.Net` evaluation to fail for an unknown JSON member.

The production importer must use both: typed YAML deserialization for front matter and a versioned JSON Schema whose object definitions set `additionalProperties: false`. JSON Schema alone cannot make YAML deserialization strict, and strict YAML deserialization cannot validate cross-field schema rules.

The positive front-matter test is required alongside the rejection tests: it prevents a naming or extraction failure from masquerading as successful unknown-property validation.

## Commands

```bash
dotnet restore spikes/platform/catalog-validation/CatalogValidation.ApiSpike.csproj
dotnet test spikes/platform/catalog-validation/CatalogValidation.ApiSpike.csproj --no-restore
```

## Primary sources

- https://github.com/xoofx/markdig
- https://xoofx.github.io/markdig
- https://github.com/aaubry/YamlDotNet
- https://github.com/aaubry/YamlDotNet/wiki/Deserialization
- https://json-schema.org/understanding-json-schema/reference/object
- https://docs.json-everything.net/schema/basics/
