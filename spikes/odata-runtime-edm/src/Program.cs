using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using ODataRuntimeEdmSpike;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddOData(options => options
        .AddRouteComponents("odata", RuntimeEdmModel.Create())
        .Select()
        .Filter()
        .OrderBy()
        .Count()
        .SetMaxTop(100));

var app = builder.Build();
app.MapControllers();
app.Run();

public partial class Program;

internal static class RuntimeEdmModel
{
    public static IEdmModel Create()
    {
        var model = new EdmModel();
        var salesOrder = new EdmEntityType("Reporting", "SalesOrder");
        var id = salesOrder.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32, isNullable: false);
        salesOrder.AddKeys(id);
        salesOrder.AddStructuralProperty("CustomerName", EdmPrimitiveTypeKind.String, isNullable: false);
        salesOrder.AddStructuralProperty("NetAmount", EdmPrimitiveTypeKind.Decimal, isNullable: false);
        model.AddElement(salesOrder);
        model.SetAnnotationValue(salesOrder, new ClrTypeAnnotation(typeof(SalesOrder)));

        var container = new EdmEntityContainer("Reporting", "Gateway");
        container.AddEntitySet("SalesOrders", salesOrder);
        model.AddElement(container);
        return model;
    }
}
