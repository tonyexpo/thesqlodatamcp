using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace ODataRuntimeEdmSpike;

[ODataRouteComponent("odata")]
public sealed class SalesOrdersController : ODataController
{
    private static readonly SalesOrder[] Rows =
    [
        new(2, "Contoso Ltd.", 980.00m),
        new(1, "Alpine Ski House", 1250.50m)
    ];

    [EnableQuery]
    public IQueryable<SalesOrder> Get() => Rows.AsQueryable();
}
