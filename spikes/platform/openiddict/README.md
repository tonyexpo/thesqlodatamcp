# OpenIddict API spike

## Selection and result

Pinned package: `OpenIddict.Server.AspNetCore` **7.6.0** (current stable at the time of this spike).

`OpenIddictApiTests` compiles and constructs a server configuration for authorization code, refresh token and revocation endpoints, global PKCE enforcement, and reference access/refresh tokens. This is an API-level proof only: it deliberately does not claim an end-to-end authorization flow, which needs the future identity/consent UI and persistent OpenIddict store.

## Important scope finding

OpenIddict 7.6.0 does **not** supply OAuth 2.0 Dynamic Client Registration (RFC 7591/7592). Its upstream tracker lists it as an open enhancement ([#2414](https://github.com/openiddict/openiddict-core/issues/2414)). Therefore the handoff's v1 requirement for dynamic public-client registration cannot be met solely by OpenIddict: implement a bounded registration endpoint and persist clients using OpenIddict's application manager, or select/add a dedicated RFC 7591 component. This is a design risk to resolve before Milestone 5.

The server exposes the proven code/PKCE, refresh, revocation and reference-token APIs. It also exposes standard resource-indicator configuration/validation APIs: `RegisterResources(...)` registers supported resource indicators and `OpenIddictApplicationDescriptor.AddResourcePermissions(...)` grants a client permission for a registered resource. The automated API proof compiles and asserts both. A custom policy is needed only if the product intends resources to be dynamically created or uses policy beyond this allowlist; it is not required for normal RFC 8707 resource-indicator validation.

## Commands

```bash
dotnet restore spikes/platform/openiddict/OpenIddict.ApiSpike.csproj
dotnet test spikes/platform/openiddict/OpenIddict.ApiSpike.csproj --no-restore
```

## Primary sources

- https://documentation.openiddict.com/configuration/proof-key-for-code-exchange
- https://documentation.openiddict.com/configuration/token-storage.html
- https://documentation.openiddict.com/guides/choosing-the-right-flow.html
- https://documentation.openiddict.com/guides/getting-started/
- https://github.com/openiddict/openiddict-core/releases/tag/7.6.0
- https://github.com/openiddict/openiddict-core/issues/2414
