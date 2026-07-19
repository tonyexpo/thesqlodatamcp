#!/usr/bin/env bash
set -euo pipefail

port=5088
endpoint="http://127.0.0.1:${port}/mcp"
protocol_version="2025-11-25"

dotnet run --project McpSdkSpike.csproj --no-build --urls "http://127.0.0.1:${port}" >/tmp/thesqlodatamcp-mcp-sdk-server.log 2>&1 &
server_pid=$!
trap 'kill "$server_pid" 2>/dev/null || true' EXIT

post_mcp() {
  curl --silent --show-error --fail --request POST "$endpoint" \
    --header 'Content-Type: application/json' \
    --header 'Accept: application/json, text/event-stream' \
    --header "MCP-Protocol-Version: ${protocol_version}" \
    --data "$1"
}

initialize_response=''
for _ in {1..30}; do
  initialize_response=$(post_mcp \
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"mcp-sdk-spike-verifier","version":"1.0"}}}' \
    2>/dev/null || true)

  if rg --quiet '"protocolVersion":"2025-11-25"' <<<"$initialize_response"; then
    break
  fi
  sleep 0.2
done

rg --quiet '"serverInfo":\{"name":"McpSdkSpike","version":"1.0.0.0"\}' <<<"$initialize_response"
rg --quiet '"capabilities":\{' <<<"$initialize_response"

post_mcp '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}' >/dev/null

tool_list=$(post_mcp '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}')

tool_call=$(post_mcp '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo_structured","arguments":{"message":"ciao"}}}')

rg --quiet '"outputSchema"' <<<"$tool_list"
rg --quiet '"message"' <<<"$tool_list"
rg --quiet '"length"' <<<"$tool_list"
rg --quiet '"structuredContent":\{"message":"ciao","length":4\}' <<<"$tool_call"

echo "MCP initialize, Streamable HTTP, generated outputSchema, and structuredContent verified."
