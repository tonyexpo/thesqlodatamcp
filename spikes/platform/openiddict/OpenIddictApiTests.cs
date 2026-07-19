using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace TheSqlODataMcp.Spikes.OpenIddict;

public sealed class OpenIddictApiTests
{
    [Fact]
    public void Server_builder_exposes_v1_required_code_pkce_refresh_revocation_and_reference_token_apis()
    {
        var services = new ServiceCollection();

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revocation")
                    .AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange()
                    .UseReferenceAccessTokens()
                    .UseReferenceRefreshTokens()
                    .RegisterResources("https://api.thesqlodatamcp.example")
                    .AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();
            });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<global::OpenIddict.Server.OpenIddictServerOptions>>().Value;

        Assert.Contains(OpenIddictConstants.GrantTypes.AuthorizationCode, options.GrantTypes);
        Assert.Contains(OpenIddictConstants.GrantTypes.RefreshToken, options.GrantTypes);
        Assert.True(options.UseReferenceAccessTokens);
        Assert.True(options.UseReferenceRefreshTokens);
        Assert.NotNull(options);
    }

    [Fact]
    public void Application_descriptor_exposes_resource_permissions_for_registered_resource_indicators()
    {
        const string resource = "https://api.thesqlodatamcp.example";
        var descriptor = new OpenIddictApplicationDescriptor();

        descriptor.AddResourcePermissions(resource);

        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Resource + resource, descriptor.Permissions);
    }
}
