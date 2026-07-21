using System.Reflection;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerTypeMapperQaTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void MapsEverySupportedTemporalScale(int scale)
    {
        var storageOffset = scale switch
        {
            <= 2 => 0,
            <= 4 => 1,
            _ => 2,
        };

        AssertTemporalMapping("time", 3 + storageOffset, scale == 0 ? 8 : 9 + scale, scale, CanonicalScalarType.Time);
        AssertTemporalMapping("datetime2", 6 + storageOffset, scale == 0 ? 19 : 20 + scale, scale, CanonicalScalarType.DateTime);
        AssertTemporalMapping("datetimeoffset", 8 + storageOffset, scale == 0 ? 26 : 27 + scale, scale, CanonicalScalarType.DateTimeOffset);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(9, 5)]
    [InlineData(10, 9)]
    [InlineData(19, 9)]
    [InlineData(20, 13)]
    [InlineData(28, 13)]
    [InlineData(29, 17)]
    [InlineData(38, 17)]
    public void MapsEveryDecimalStorageBoundary(int precision, int maxLength)
    {
        var mapping = SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata("decimal", maxLength, precision, precision));

        Assert.Equal(CanonicalScalarType.Decimal, mapping.CanonicalType);
        Assert.Equal($"decimal({precision},{precision})", mapping.ProviderType.StoreRepresentation);
        Assert.Equal(precision, mapping.ProviderType.Precision);
        Assert.Equal(precision, mapping.ProviderType.Scale);
    }

    [Theory]
    [InlineData(24, 4)]
    [InlineData(25, 8)]
    public void MapsBothFloatStorageBands(int precision, int maxLength)
    {
        var mapping = SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata("float", maxLength, precision, 0));

        Assert.Equal(CanonicalScalarType.Double, mapping.CanonicalType);
        Assert.Equal($"float({precision})", mapping.ProviderType.StoreRepresentation);
        Assert.Equal(precision, mapping.ProviderType.Precision);
    }

    [Fact]
    public void NormalizesUnknownProviderNamesWithoutInventingMetadata()
    {
        var mapping = SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata("  Vendor\t  Custom Type  ", 128, 42, 7));

        Assert.Equal(CanonicalScalarType.Unknown, mapping.CanonicalType);
        Assert.Equal("vendor custom type", mapping.ProviderType.Name);
        Assert.Equal("vendor custom type", mapping.ProviderType.StoreRepresentation);
        Assert.Null(mapping.ProviderType.Length);
        Assert.Null(mapping.ProviderType.Precision);
        Assert.Null(mapping.ProviderType.Scale);
    }

    [Theory]
    [InlineData(-2, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, -1)]
    public void RejectsInvalidCommonMetadataForUnknownTypes(int maxLength, int precision, int scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata("vendor_type", maxLength, precision, scale)));
    }

    [Fact]
    public void RejectsNullMetadata()
    {
        Assert.Throws<ArgumentNullException>(() => SqlServerTypeMapper.Map(null!));
    }

    [Fact]
    public void PublicMappingApiDoesNotExposeSqlClientTypes()
    {
        var publicApiTypes = typeof(SqlServerTypeMapper).Assembly.ExportedTypes
            .SelectMany(type =>
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Append(method.ReturnType))
                    .Concat(type.GetConstructors().SelectMany(constructor =>
                        constructor.GetParameters().Select(parameter => parameter.ParameterType)))
                    .Concat(type.GetProperties().Select(property => property.PropertyType)))
            .Distinct();

        Assert.DoesNotContain(publicApiTypes, type =>
            string.Equals(type.Assembly.GetName().Name, "Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    private static void AssertTemporalMapping(
        string typeName,
        int maxLength,
        int precision,
        int scale,
        CanonicalScalarType canonicalType)
    {
        var mapping = SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata(typeName, maxLength, precision, scale));

        Assert.Equal(canonicalType, mapping.CanonicalType);
        Assert.Equal($"{typeName}({scale})", mapping.ProviderType.StoreRepresentation);
        Assert.Equal(scale, mapping.ProviderType.Scale);
    }
}
