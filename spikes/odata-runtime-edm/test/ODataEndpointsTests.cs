using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Xunit;

namespace ODataRuntimeEdmSpike.Tests;

public sealed class ODataEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ServiceDocument_exposes_the_runtime_entity_set()
    {
        using var response = await factory.CreateClient().GetAsync("/odata");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("SalesOrders", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Metadata_exposes_the_runtime_edm_schema()
    {
        using var response = await factory.CreateClient().GetAsync("/odata/$metadata");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("EntityType Name=\"SalesOrder\"", body, StringComparison.Ordinal);
        Assert.Contains("EntitySet Name=\"SalesOrders\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entity_set_get_applies_odata_orderby_to_rows_defined_outside_an_ef_model()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/odata/SalesOrders?$orderby=Id");
        request.Headers.TryAddWithoutValidation("Accept", "application/json;odata.metadata=minimal");
        using var response = await factory.CreateClient().SendAsync(request);

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);
        var root = document.RootElement;
        Assert.Equal(
            "http://localhost/odata/$metadata#SalesOrders",
            root.GetProperty("@odata.context").GetString());
        Assert.Equal(
            [1, 2],
            root.GetProperty("value").EnumerateArray()
                .Select(row => row.GetProperty("Id").GetInt32())
                .ToArray());
    }
}
