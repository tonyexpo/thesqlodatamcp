# OData runtime EDM spike

## Question

Can ASP.NET Core OData run on .NET 10 and publish an EDM constructed at runtime,
without an EF model for the reporting source?

## Result

**Yes, for the tested baseline.** The app targets `net10.0`, pins
`Microsoft.AspNetCore.OData` **9.5.0**, builds an `EdmModel` directly with
`Microsoft.OData.Edm`, and registers it via `AddRouteComponents`. It does not
reference EF Core. The `SalesOrder` record is merely a transport DTO for the
sample response; it is not the source schema and does not generate the EDM.

The test suite proves these endpoints:

- `GET /odata` — OData service document with `SalesOrders`.
- `GET /odata/$metadata` — XML CSDL containing the runtime `SalesOrder` type
  and `SalesOrders` entity set.
- `GET /odata/SalesOrders?$orderby=Id` — OData JSON response with an
  `@odata.context` annotation and rows ordered by `Id`, even though the sample
  source array is deliberately in the reverse order.

## Run

From this directory:

```bash
dotnet restore test/ODataRuntimeEdmSpike.Tests.csproj
dotnet build test/ODataRuntimeEdmSpike.Tests.csproj --no-restore
dotnet test test/ODataRuntimeEdmSpike.Tests.csproj --no-build
dotnet run --project src/ODataRuntimeEdmSpike.csproj
```

The test run hosts the application in-process. For a manual check, browse the
three paths above after `dotnet run`.

## Verification evidence

Executed on 2026-07-19 with .NET SDK `10.0.109` and ASP.NET Core runtime
`10.0.9`:

```text
dotnet restore test/ODataRuntimeEdmSpike.Tests.csproj  # succeeded
dotnet build test/ODataRuntimeEdmSpike.Tests.csproj --no-restore  # succeeded, 0 warnings
dotnet test test/ODataRuntimeEdmSpike.Tests.csproj --no-build  # 3 passed, 0 failed
```

## Package baseline

| Package | Version | Why |
| --- | ---: | --- |
| `Microsoft.AspNetCore.OData` | 9.5.0 | Latest stable package at spike time; its NuGet metadata targets .NET 8 or higher, so this spike verifies actual execution on .NET 10. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.9 | Matches the installed .NET 10 ASP.NET Core runtime for in-process endpoint tests. |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host. |
| `xunit` | 2.9.3 | Test framework. |
| `xunit.runner.visualstudio` | 3.1.4 | VSTest adapter. |

## Limits and follow-up

This only establishes the host/EDM seam. It deliberately does **not** validate
the v1 OData capability matrix, CQM translation, authorization, runtime
catalog refresh, SQL Server execution, or Power BI interoperability. The
product adapter must keep all filtering and query semantics constrained to the
CQM; the spike's `EnableQuery` controller attribute is not approval to expose
arbitrary LINQ/provider queries in the product.


Before adoption, repeat the endpoint tests against a runtime catalog generated
from SQL Server metadata and verify that model replacement is atomic.

## Primary sources

- [Microsoft.AspNetCore.OData 9.5.0 — official NuGet package](https://www.nuget.org/packages/Microsoft.AspNetCore.OData/9.5.0)
- [ASP.NET Core OData fundamentals overview](https://learn.microsoft.com/en-us/odata/webapi-8/fundamentals/overview)
- [OData library support policy](https://learn.microsoft.com/en-us/odata/support/support-policy)
- [Official ASP.NET Core OData repository](https://github.com/OData/AspNetCoreOData)

The first source records the package target as .NET 8 or higher. The test
commands above are the compatibility evidence for the installed .NET 10 SDK
(`10.0.109`) and ASP.NET Core runtime (`10.0.9`).
