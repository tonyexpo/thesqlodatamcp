using System.Reflection;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.Protocols;
using TheSqlODataMcp.Web;
using Xunit;

namespace TheSqlODataMcp.IntegrationTests;

/// <summary>
/// SQL-01c: protocol and HTTP adapter surfaces must resolve catalog metadata through
/// <see cref="CatalogReadView"/>. They may not expose the internal merged/technical catalog types, which would let a
/// caller bypass the exposed-only filter and reach hidden objects.
/// </summary>
public sealed class CatalogBoundaryTests
{
    private static readonly Type[] InternalCatalogTypes = [typeof(MergedCatalog), typeof(TechnicalCatalog)];

    [Fact]
    public void ProtocolAndWebSurfacesDoNotExposeInternalCatalogTypes()
    {
        var assemblies = new[]
        {
            typeof(ProtocolsAssemblyMarker).Assembly,
            typeof(WebAssemblyMarker).Assembly,
        };

        var violations = new List<string>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                InspectType(type, violations);
            }
        }

        Assert.Empty(violations);
    }

    private static void InspectType(Type type, List<string> violations)
    {
        if (ContainsInternalCatalogType(type.BaseType))
        {
            violations.Add($"{type.FullName} derives from an internal catalog type.");
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (ContainsInternalCatalogType(contract))
            {
                violations.Add($"{type.FullName} implements {contract.FullName}.");
            }
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var member in type.GetMembers(flags))
        {
            switch (member)
            {
                case MethodInfo method when ContainsInternalCatalogType(method.ReturnType)
                    || method.GetParameters().Any(parameter => ContainsInternalCatalogType(parameter.ParameterType)):
                    violations.Add($"{type.FullName}.{method.Name} exposes an internal catalog type.");
                    break;
                case ConstructorInfo constructor when constructor.GetParameters().Any(parameter => ContainsInternalCatalogType(parameter.ParameterType)):
                    violations.Add($"{type.FullName} constructor exposes an internal catalog type.");
                    break;
                case PropertyInfo property when ContainsInternalCatalogType(property.PropertyType)
                    || property.GetIndexParameters().Any(parameter => ContainsInternalCatalogType(parameter.ParameterType)):
                    violations.Add($"{type.FullName}.{property.Name} exposes an internal catalog type.");
                    break;
                case FieldInfo field when ContainsInternalCatalogType(field.FieldType):
                    violations.Add($"{type.FullName}.{field.Name} exposes an internal catalog type.");
                    break;
                case EventInfo @event when ContainsInternalCatalogType(@event.EventHandlerType):
                    violations.Add($"{type.FullName}.{@event.Name} exposes an internal catalog type.");
                    break;
            }
        }
    }

    private static bool ContainsInternalCatalogType(Type? type)
    {
        if (type is null || type.IsGenericParameter)
        {
            return false;
        }

        if (type.IsArray)
        {
            return ContainsInternalCatalogType(type.GetElementType());
        }

        if (type.IsByRef || type.IsPointer)
        {
            return ContainsInternalCatalogType(type.GetElementType());
        }

        if (InternalCatalogTypes.Contains(type))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsInternalCatalogType);
    }
}
