# MCP SDK spike

Minimal, executable proof that the official MCP C# SDK can host an ASP.NET
Core/.NET 10 server over Streamable HTTP and generate a tool output schema plus
`structuredContent` from a typed C# result.

## Decision tested

- Package: `ModelContextProtocol.AspNetCore` **1.4.1**, explicitly pinned in
  `McpSdkSpike.csproj`.
- Transport: Streamable HTTP at `POST /mcp`, configured statelessly.
- Tool registration: attribute-based `McpServerTool` registration.
- Output: `UseStructuredContent = true` causes the SDK to infer `outputSchema`
  from `EchoResult` and emit a matching `structuredContent` object.

`1.4.1` was the current stable release when this spike was run (2026-07-19).
The simultaneously available 2.0 releases were previews, so are deliberately
not used for a v1 product baseline.

## Reproduce

The following keeps CLI state and NuGet packages outside the repository. Run
from the repository root:

```bash
mkdir -p /tmp/thesqlodatamcp-mcp-sdk-dotnet /tmp/thesqlodatamcp-mcp-sdk-nuget
DOTNET_CLI_HOME=/tmp/thesqlodatamcp-mcp-sdk-dotnet \
NUGET_PACKAGES=/tmp/thesqlodatamcp-mcp-sdk-nuget \
dotnet restore spikes/mcp-sdk/McpSdkSpike.csproj
DOTNET_CLI_HOME=/tmp/thesqlodatamcp-mcp-sdk-dotnet \
NUGET_PACKAGES=/tmp/thesqlodatamcp-mcp-sdk-nuget \
dotnet build spikes/mcp-sdk/McpSdkSpike.csproj --no-restore
(cd spikes/mcp-sdk && DOTNET_CLI_HOME=/tmp/thesqlodatamcp-mcp-sdk-dotnet \
  NUGET_PACKAGES=/tmp/thesqlodatamcp-mcp-sdk-nuget ./verify.sh)
```

`verify.sh` starts the server only on `127.0.0.1:5088`. Its readiness probe is
a real MCP `initialize` request for protocol version `2025-11-25` from a named
client; it asserts the negotiated version, server identity, and capabilities.
It then sends `notifications/initialized` and calls `tools/list` and
`tools/call`, each with the `MCP-Protocol-Version` header. The assertions cover
both the generated `outputSchema` and the returned `structuredContent`.

## Observed result

The live protocol run negotiated `2025-11-25` and returned the server identity
`McpSdkSpike` with its advertised capabilities. `tools/list` then returned an
`outputSchema` with required `message` (string) and `length` (integer)
properties. A `tools/call` with `{"message":"ciao"}` returned a compact text
representation and:

```json
"structuredContent": { "message": "ciao", "length": 4 }
```

## Limits and follow-up

- This is transport and DTO-shape validation only: no OAuth, catalog, CQM,
  database connection, authorization, rate limits, or production observability.
- Stateless transport is appropriate for this proof because it does not need
  unsolicited server-to-client messages. The real host must revisit this choice
  if it adds MCP capabilities that require sessions.
- The next MCP implementation should preserve strict CQM input/output contracts
  and add protocol tests through the final solution test boundaries; this spike
  does not establish the production tool surface.
- `AllowedHosts` is intentionally loopback-only for local execution. Production
  host allow-listing belongs to the web-host configuration work.

## Primary sources

- [Official MCP C# SDK repository](https://github.com/modelcontextprotocol/csharp-sdk)
  — identifies `ModelContextProtocol.AspNetCore` as the HTTP server package.
- [Official SDK transport documentation](https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html)
  — `AddMcpServer`, `WithHttpTransport`, `MapMcp`, Streamable HTTP and
  stateless-mode guidance.
- [Official SDK tool documentation](https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html)
  — attribute-based tool registration and generated JSON Schema inputs.
- [Official SDK 1.4.1 release](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v1.4.1)
  — pinned stable SDK release.
- [Official MCP specification repository](https://github.com/modelcontextprotocol/modelcontextprotocol)
  — `tools/list` output schema and `tools/call` structured-content protocol
  fields.
