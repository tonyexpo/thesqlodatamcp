# QA Analysis & Handoff — Claude (external QA review)

**Role**: external, consultative QA pass on the codebase as it stands at commit `2ab450c` (branch `main`, clean working tree). No source files were modified as part of this review — this document is the only artifact added. Verification claims below were checked by actually building, running, and testing the project (and a couple of small throwaway scratch projects outside this repo), not by reading code alone; each finding says how it was verified.

**Context for whoever picks this up next (including a future qwen session)**: this project and `llama-mcp` (a sibling project, same author) are both MCP servers built on the official `ModelContextProtocol` NuGet package, but on very different preview versions with different API shapes — see the version note in Finding 1. Don't assume the two are API-compatible.

---

## 1. Root cause: the "internal methods" build blocker

### What's actually pinned

`thesqlodatamcp.csproj` references:
```
ModelContextProtocol Version="0.1.0-preview.1.25171.12"
```
This is a very early preview (versioned by build date, `25171` ≈ day 171 of 2025) with a **different, older API shape** than later previews — e.g. `llama-mcp`'s `2.0.0-preview.1` uses `[McpServerToolType]`/`[McpServerTool]` attribute names; this version uses `[McpToolType]`/`[McpTool]` instead (confirmed by loading both cached package assemblies and enumerating their public types via reflection — see Appendix A for method). They are not drop-in equivalent APIs; don't port code between the two projects assuming the names line up.

### What actually happened (evidence, not speculation)

Commit `9aea424` ("feat: complete Phases 3, 4, and 5 …") added a real attempt to wire up the MCP server, then the very next commits (`95f559c`, `2ab450c`) ripped it back out to the current `Console.WriteLine` placeholder in `Program.cs` — the changelog frames this as the SDK "not exposing" the needed types. That framing is incorrect, and the diff of the reverted attempt (`git show 9aea424 -- Program.cs`) shows exactly why the build actually failed. The removed code did **all** of the following:

```csharp
using ModelContextProtocol.Transport.Stdio;   // ← namespace does not exist in this package at all
...
var server = new McpServer(serverOptions);    // ← McpServer is `internal` — this is your "internal methods" error
...
var listTablesToolDef = new ToolDefinition(...);      // ← type does not exist
server.AddTool(listTablesToolDef, async (toolCall, ctx) => { ... });  // ← method does not exist
var transport = new StdioServerTransport();   // ← real type, but no parameterless constructor exists
await server.StartAsync(transport, default);  // ← wrong overload/shape
```

None of `ToolDefinition`, `ToolResultResponse`, `ToolResult`, `server.AddTool(...)`, or a parameterless `StdioServerTransport()` exist anywhere in this package version (confirmed by enumerating **every public type** in the assembly — 237 types total, full list in Appendix A). This isn't a version-skew edge case; it's a different, invented API that doesn't match any real version of the SDK. `McpServer` genuinely is `internal` (confirmed), so `new McpServer(...)` genuinely does fail with `CS0122 (inaccessible due to its protection level)` — that's almost certainly the literal "internal methods" error reported. But the correct read of that error is **"you're constructing the wrong type directly — use the public factory/DI entry point instead,"** not "the SDK doesn't support this." The model hit one real compiler error, diagnosed it as an SDK limitation instead of a wrong-entry-point mistake, and gave up rather than inspecting what the package actually exposes.

### What the real public API looks like (verified by reflection against the exact pinned package)

Two supported ways to stand this server up, both fully public, no internal types touched:

**A. DI / Generic Host path (idiomatic for this SDK version)**
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new Implementation { Name = "TheSqlODataMCP", Version = "1.0.0" };
})
.WithStdioServerTransport()
.WithToolsFromAssembly();   // scans for [McpToolType] classes / [McpTool] methods

await builder.Build().RunAsync();
```
`AddMcpServer` is a real extension method on `IServiceCollection` (`ModelContextProtocol.McpServerServiceCollectionExtension`); `.WithStdioServerTransport()` / `.WithToolsFromAssembly()` are real extensions on the returned `IMcpServerBuilder` (`ModelContextProtocol.McpServerBuilderExtensions`). Under the hood this registers `ModelContextProtocol.Hosting.McpServerHostedService` (a `BackgroundService`/`IHostedService`), which is exactly what `Host...RunAsync()` drives. `McpTools` would need `[McpToolType]` on the class and `[McpTool("list_tables")]` etc. on each method (this version's attribute names — **not** `[McpServerToolType]`/`[McpServerTool]`, which belong to later SDK versions).

**B. Fully manual path (no DI, if the Host pattern is undesirable)**
```csharp
var loggerFactory = LoggerFactory.Create(b => { });
var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "TheSqlODataMCP", Version = "1.0.0" },
    Capabilities = new ServerCapabilities
    {
        Tools = new ToolsCapability
        {
            ListToolsHandler = (ctx, ct) => ...,   // build ListToolsResult by hand
            CallToolHandler  = (ctx, ct) => ...,   // dispatch on ctx.Params.Name by hand
        }
    }
};
var transport = new StdioServerTransport(options, loggerFactory);   // public ctor, confirmed
var server = McpServerFactory.Create(transport, options, loggerFactory, serviceProvider); // public factory, confirmed
await server.StartAsync(cancellationToken);   // IMcpServer.StartAsync, confirmed public
```
This avoids attribute-based discovery (more boilerplate, no DI needed) but is fully achievable with public API only — useful if the goal is to prove out the concept without restructuring `Program.cs` around a Host.

**Practical prerequisite neither path currently has**: the project's `.csproj` only lists `ModelContextProtocol` and `Microsoft.Data.SqlClient` as direct dependencies. Only the `.Abstractions` flavors of `Microsoft.Extensions.DependencyInjection`/`Microsoft.Extensions.Hosting` come in transitively through `ModelContextProtocol` itself (confirmed by inspecting `thesqlodatamcp.deps.json` after a real build) — the **concrete** implementations (`Microsoft.Extensions.Hosting` for `Host.CreateApplicationBuilder`, or at minimum `Microsoft.Extensions.DependencyInjection` for a bare `ServiceCollection`) are not pulled in at all today. Path A needs an explicit `<PackageReference Include="Microsoft.Extensions.Hosting" .../>` added before it will even compile.

### Recommendation

Two options, in order of preference:
1. **Stay on the pinned `0.1.0-preview.1.25171.12`** and implement Path A above (add the `Microsoft.Extensions.Hosting` package, restructure `Program.cs` around `Host.CreateApplicationBuilder`, rename the `[McpTool]`/`[McpToolType]` attributes onto `McpTools.cs`'s methods). This is the smaller change and everything needed for it was verified present in the currently-pinned package — no upgrade risk.
2. **Upgrade to a current preview** (e.g. the `2.0.0-preview.1` line already proven working end-to-end in the sibling `llama-mcp` project) if the newer, more actively-maintained API surface (`AddMcpServer().WithStdioServerTransport().WithTools<T>()`, `[McpServerToolType]`/`[McpServerTool]`) is preferred going forward. Don't assume the API is identical to `llama-mcp`'s HTTP-transport setup — re-verify the stdio-specific extension methods for whichever version is picked, the same way this review did (Appendix A method), rather than porting code by name-guessing.

Either way: **don't hand this back to a model with instructions to "just implement the MCP SDK integration" without pointing it at the actual package's public surface.** The failure mode here wasn't the SDK — it was inventing a plausible-looking API and never checking it against the real assembly. A cheap guardrail for next time: `dotnet build` after every SDK-facing change (not just at the end of a work session), and if a compile error mentions "inaccessible due to its protection level," that's a signal to look for the public factory/builder next to the internal type, not to abandon the approach.

---

## 2. Security findings

The project's entire stated purpose is guaranteeing **read-only, DQL-only** DB access (see `docs/architecture.md`, point 3). Two bypasses were found and **confirmed by running the actual, unmodified `DqlValidator.cs` against test inputs** (via a throwaway scratch console app referencing the real file directly — not just static reading):

### 2.1 `SELECT ... INTO` bypasses DQL-only enforcement (High)
```
Input:  SELECT * INTO EvilTable FROM Users
Result: PASSES validation
```
`SELECT INTO` is valid T-SQL that **creates a new table** — a DDL side effect — while starting with `SELECT` and containing none of the blacklisted keywords (`INSERT/UPDATE/DELETE/DROP/ALTER/CREATE/TRUNCATE/...` — `INTO` isn't on that list) or blacklisted patterns (`UNION/EXCEPT/INTERSECT/EXEC`). Since `execute_dql_query` builds `SELECT * FROM [{tableName}]` and only appends caller-supplied WHERE conditions, an attacker would need control over the where-conditions text with a literal SQL snippet (the `WHERE`-prefixed free-text path in `McpTools.ExecuteDqlQueryAsync`) to reach this — but that path exists today, unauthenticated beyond the single static bearer token.

### 2.2 `WAITFOR DELAY` bypasses DQL-only enforcement (Medium — blind/time-based DoS)
```
Input:  SELECT * FROM Users WHERE 1=1; WAITFOR DELAY '0:0:10'--
Result: PASSES validation
```
Same root cause: `WAITFOR` isn't in `ForbiddenKeywords`. This allows a caller to block the connection for an arbitrary delay, and is the classic building block for time-based blind SQL injection probing even where direct data exfiltration is blocked elsewhere.

**Root cause for both**: `DqlValidator` is a hand-rolled keyword/regex blacklist, not a real T-SQL parser — it can only ever block constructs someone thought to enumerate. This is already acknowledged in `docs/backlog.md`'s vNext list ("Advanced security features beyond the T-SQL DQL Parser (e.g., AST-based SQL parsing)"), so the team is aware the approach is inherently incomplete — but "incomplete" here means "known, exploitable bypasses exist today with the current tool set," not just theoretical future hardening. `docs/backlog.md` Phase 6 also lists "Verify security (SQL injection prevention, DQL only enforcement) in integration with McpTools" as unchecked — this review's two findings are exactly what that verification step would have caught.

Suggested minimum fix before any real deployment: add `INTO`, `MERGE`, `WAITFOR`, `OPENROWSET`, `OPENQUERY`, `BULK`, `BACKUP`, `RESTORE`, `SHUTDOWN`, `KILL`, `DBCC` to `ForbiddenKeywords`. This is still a blacklist (same fundamental weakness), but closes the two concretely demonstrated holes; a real fix is the AST-based parser already on the backlog.

### 2.3 Table-name interpolation doesn't escape `]` (Low/Medium, not independently confirmed exploitable)
`McpTools.ExecuteDqlQueryAsync` builds `$"SELECT * FROM [{tableName}]"` — bracket-quoting without doubling an embedded `]` (the real T-SQL escape for it) means a `tableName` containing `]` can break out of the identifier and inject into the query text. In practice the *final* assembled string still passes through `IsValidDql` before execution, which catches most classic injections (anything hitting the keyword blacklist) — but it inherits the exact same blacklist gaps as 2.1/2.2, so a table name like `Users] WHERE 1=1; WAITFOR DELAY '0:0:5'--` would reach the DB. Not independently re-tested against a live SQL Server in this review (none was available in this environment) — flagged as a code-review-level observation, worth a quick integration test alongside fixing 2.1/2.2.

### 2.4 `settings.json` field names don't match what `AppSettings` actually deserializes (High — functional bug, not just security, confirmed live)
The checked-in `settings.json`:
```json
{
  "BearerToken": "your-bearer-token-here",
  "SqlConnectionStr": "Server=localhost;Database=YourDatabase;Trusted_Connection=True;",
  "AuthSettingsFile": "settings.json"
}
```
`AppSettings` (`Settings.cs`) expects `bearerToken`, `sqlConnectionString`, `authSettingsFileName` (case-insensitive matching is on, but the *names* themselves differ — `SqlConnectionStr` vs `sqlConnectionString`, `AuthSettingsFile` vs `authSettingsFileName`). **Confirmed by actually running the app** (`dotnet run`) against the exact checked-in file:
```
Bearer Token: your-bearer-token-here
SQL Connection String:
```
The connection string silently deserializes to `""`. `Program.cs` validates `BearerToken` is non-empty but never validates `SqlConnectionString` — so the app reports "Bearer token authentication validated successfully" and proceeds, and the failure only surfaces much later, deep inside `DatabaseConnector`, as an opaque ADO.NET connection error. This will bite the very first person who copies `settings.json`, fills in real values under the existing key names, and runs it. Fix is a one-line rename in `settings.json` (or in the `JsonPropertyName` attributes, whichever is meant to be the source of truth) plus a startup check that `SqlConnectionString` is non-empty, mirroring the existing `BearerToken` check.

---

## 3. Minor / code-quality observations (not blocking, worth a look)

- **`McpTools.ExecuteDqlQueryAsync`'s JSON condition parsing coerces every value to a string** before parameterizing (`property.Value.GetInt32().ToString()` → `parameters[paramKey] = valueStr`, always a `string`). A JSON int like `{"age": 30}` ends up bound as the *string* `"30"` against what's presumably an `int` column — SQL Server will usually implicitly convert, but this can hurt index usage (implicit conversion on the parameter side) and will outright misbehave against column types that don't implicitly convert from `nvarchar` the way `int` does. Also, any JSON value that isn't a string or a whole number (float, bool, null, nested object/array) hits `GetInt32()` and throws — currently swallowed and rethrown as a generic "Invalid JSON conditions format," which is accurate-enough but will confuse whoever passes `{"price": 19.99}` expecting it to work.
- **`SqlCommand.Parameters.AddWithValue`** is used throughout `DatabaseConnector.cs`. Not a vulnerability (values are still parameterized, not concatenated), but it's a known anti-pattern because the CLR-inferred `SqlDbType` can silently mismatch the column type, causing implicit-conversion query plan issues. Prefer explicit `SqlParameter` with a declared type/size where the column type is known (schema queries already know it via `GetTableSchema`).
- **`McpTools.GetTableSchemaAsync` returns `Task<List<(string columnName, string dataType)>>`** — a `ValueTuple` return shape. Once this is actually wired into the MCP SDK's tool-result serialization (Section 1), tuples don't have a natural JSON Schema/property-name representation the way a named DTO class would — worth switching to a small record/class (`record ColumnInfo(string ColumnName, string DataType)`) before wiring up real tool registration, both for cleaner JSON output to MCP clients and because attribute-based tool discovery (`WithToolsFromAssembly`) generates the tool's input/output schema from the method signature.
- **Nullable warnings already surfaced by the compiler** (`McpTools.cs:52` `CS8600`, `:67` `CS8601`) around `property.Value.GetString()` possibly being `null` when assigned to a non-nullable `string`. Low risk today (`GetInt32().ToString()` never returns null; `GetString()` can, for a JSON `null` value, which would then get boxed as `object` and handled OK downstream by the `?? DBNull.Value` in `DatabaseConnector`) — but worth silencing correctly (a null-forgiving `!` or explicit null handling) rather than leaving warnings in a security-relevant code path.
- **`thesqlodatamcp.Tests/bin/` and `thesqlodatamcp.Tests/obj/` are tracked in git** despite `.gitignore` excluding `bin/`/`obj/` project-wide (confirmed directly: running `dotnet test` this session modified two dozen tracked files under those paths). These were almost certainly committed before the `.gitignore` rule was added (`b55e355`) and never untracked afterward — `.gitignore` only stops *new* files from being added, it doesn't retroactively untrack existing ones. Worth `git rm -r --cached thesqlodatamcp.Tests/bin thesqlodatamcp.Tests/obj` once, so every future test run stops producing noisy binary diffs.
- **`settings.json` is tracked in git** with obvious placeholder values (`your-bearer-token-here`). Fine as a template today, but there's no `.gitignore` entry for it and no separate `settings.example.json` convention — worth splitting those apart *before* anyone fills in a real bearer token or connection string locally, to avoid an accidental real-secret commit later. `.gitignore` currently only excludes `bin/`, `obj/`, IDE folders, and test results.

---

## 4. Test coverage

`dotnet test thesqlodatamcp.Tests` (verified this session): **13/13 passing**, ~200ms. Coverage is solid for what it targets — `DqlValidatorTests.cs` covers the "must start with SELECT," UNION, subquery, and basic DML/DDL-keyword rejection paths well — but it does **not** cover the two bypasses in 2.1/2.2 (`SELECT INTO`, `WAITFOR`), which is exactly why they weren't caught earlier. `SettingsManagerTests.cs` tests deserialization using the *correct* camelCase field names directly in the test's inline JSON — it never exercises the actual checked-in `settings.json` file, which is why the 2.4 mismatch shipped without a failing test. Two concrete additions worth making:
1. `DqlValidatorTests`: add cases for `SELECT ... INTO ...` and `WAITFOR DELAY` (and any other keyword added per the 2.2 fix) — should throw, currently don't.
2. `SettingsManagerTests`: add a test that loads the actual `settings.json` file checked into the repo root (not just inline JSON with different key names) — would have caught 2.4 immediately.

---

## 5. Suggested priority order for the next work session

1. Fix `settings.json` field-name mismatch (2.4) — smallest change, currently-silent data-loss bug.
2. Add the missing `ForbiddenKeywords` entries (2.2) and re-run/extend `DqlValidatorTests` (2.1/2.2) — closes the two concretely demonstrated DQL bypasses.
3. Pick one of the two SDK paths in Section 1 and actually wire up `Program.cs` — this is the item that's been stuck longest and is what triggered this review; the public API to do it was confirmed present in the currently-pinned package version, so no dependency upgrade is strictly required to unblock it.
4. Once real tool registration exists, revisit the `ValueTuple` return type (Section 3) before it becomes load-bearing in a generated JSON schema.

---

## Appendix A — how the SDK's actual public API was verified

Both cached package versions (`0.1.0-preview.1.25171.12`, the one pinned here, and `2.0.0-preview.1`, used by the sibling `llama-mcp` project) are present in the local NuGet cache (`~/.nuget/packages/modelcontextprotocol/`). A throwaway console project (outside this repo, in a scratch directory — not committed anywhere) referenced the exact pinned version, loaded the assembly at runtime, and enumerated every type via `Assembly.GetTypes()` (with a `ReflectionTypeLoadException` fallback to `ex.Types` for the handful that couldn't fully load without their DI/hosting dependencies present), printing `IsPublic`, constructors, and members for the `Server`/`Transport` namespaces specifically. This is how `McpServer`'s `internal` visibility, `StdioServerTransport`'s three real (non-parameterless) public constructors, `McpServerFactory.Create(...)`'s exact signature, `IMcpServer.StartAsync(CancellationToken)`, and the full absence of `ToolDefinition`/`ToolResultResponse`/`ToolResult`/`AddTool` anywhere in the 237-type assembly were all confirmed directly against the real binary rather than inferred from documentation or the changelog's own (incorrect) claims about what the package exposes.

The two `DqlValidator` bypasses in Section 2.1/2.2 were confirmed the same way — a throwaway project referencing this repo's actual, unmodified `DqlValidator.cs` file directly (via a `Compile Include`, no copy/paste, no reimplementation) and calling `IsValidDql(...)` with the exact query strings shown.
