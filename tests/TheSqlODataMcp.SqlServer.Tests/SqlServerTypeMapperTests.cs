using System.Globalization;
using TheSqlODataMcp.Core.Catalog;
using TheSqlODataMcp.SqlServer;
using Xunit;

namespace TheSqlODataMcp.SqlServer.Tests;

public sealed class SqlServerTypeMapperTests
{
    private static readonly IReadOnlyList<TypeMappingExpectation> KnownMappings =
    [
        Type("bit", 1, 1, 0, CanonicalScalarType.Boolean, "bit"),
        Type("tinyint", 1, 3, 0, CanonicalScalarType.Int16, "tinyint"),
        Type("smallint", 2, 5, 0, CanonicalScalarType.Int16, "smallint"),
        Type("int", 4, 10, 0, CanonicalScalarType.Int32, "int"),
        Type("bigint", 8, 19, 0, CanonicalScalarType.Int64, "bigint"),
        Type("decimal", 9, 18, 4, CanonicalScalarType.Decimal, "decimal(18,4)", precision: 18, scale: 4),
        Type("numeric", 9, 18, 4, CanonicalScalarType.Decimal, "numeric(18,4)", precision: 18, scale: 4),
        Type("money", 8, 19, 4, CanonicalScalarType.Decimal, "money", precision: 19, scale: 4),
        Type("smallmoney", 4, 10, 4, CanonicalScalarType.Decimal, "smallmoney", precision: 10, scale: 4),
        Type("real", 4, 24, 0, CanonicalScalarType.Double, "real", precision: 24),
        Type("float", 8, 53, 0, CanonicalScalarType.Double, "float(53)", precision: 53),
        Type("char", 4, 0, 0, CanonicalScalarType.String, "char(4)", length: 4),
        Type("varchar", 40, 0, 0, CanonicalScalarType.String, "varchar(40)", length: 40),
        Type("varchar", -1, 0, 0, CanonicalScalarType.String, "varchar(max)"),
        Type("text", 16, 0, 0, CanonicalScalarType.String, "text"),
        Type("nchar", 8, 0, 0, CanonicalScalarType.String, "nchar(4)", length: 4),
        Type("nvarchar", 80, 0, 0, CanonicalScalarType.String, "nvarchar(40)", length: 40),
        Type("nvarchar", -1, 0, 0, CanonicalScalarType.String, "nvarchar(max)"),
        Type("ntext", 16, 0, 0, CanonicalScalarType.String, "ntext"),
        Type("uniqueidentifier", 16, 0, 0, CanonicalScalarType.Guid, "uniqueidentifier"),
        Type("date", 3, 10, 0, CanonicalScalarType.Date, "date"),
        Type("time", 4, 12, 3, CanonicalScalarType.Time, "time(3)", scale: 3),
        Type("datetime", 8, 23, 3, CanonicalScalarType.DateTime, "datetime"),
        Type("smalldatetime", 4, 16, 0, CanonicalScalarType.DateTime, "smalldatetime"),
        Type("datetime2", 7, 23, 3, CanonicalScalarType.DateTime, "datetime2(3)", scale: 3),
        Type("datetimeoffset", 8, 26, 0, CanonicalScalarType.DateTimeOffset, "datetimeoffset(0)", scale: 0),
        Type("binary", 4, 0, 0, CanonicalScalarType.Binary, "binary(4)", length: 4),
        Type("varbinary", 16, 0, 0, CanonicalScalarType.Binary, "varbinary(16)", length: 16),
        Type("varbinary", -1, 0, 0, CanonicalScalarType.Binary, "varbinary(max)"),
        Type("image", 16, 0, 0, CanonicalScalarType.Binary, "image"),
        Type("timestamp", 8, 0, 0, CanonicalScalarType.Binary, "timestamp"),
        Type("rowversion", 8, 0, 0, CanonicalScalarType.Binary, "rowversion"),
    ];

    private static readonly IReadOnlyList<UnknownMappingExpectation> UnknownMappings =
    [
        new("xml", -1, 0, 0, "xml"),
        new("sql_variant", 8016, 0, 0, "sql_variant"),
        new("hierarchyid", 892, 0, 0, "hierarchyid"),
        new("geometry", -1, 0, 0, "geometry"),
        new("dbo.FixtureUdt", -1, 999, 0, "dbo.fixtureudt"),
    ];

    [Fact]
    public void MapsFixtureAndSupportedSqlServerTypesDeterministically()
    {
        foreach (var expectation in KnownMappings)
        {
            var mapping = SqlServerTypeMapper.Map(expectation.Metadata);

            Assert.Equal(expectation.CanonicalType, mapping.CanonicalType);
            Assert.Equal(expectation.Metadata.ProviderTypeName, mapping.ProviderType.Name);
            Assert.Equal(expectation.StoreRepresentation, mapping.ProviderType.StoreRepresentation);
            Assert.Equal(expectation.Length, mapping.ProviderType.Length);
            Assert.Equal(expectation.Precision, mapping.ProviderType.Precision);
            Assert.Equal(expectation.Scale, mapping.ProviderType.Scale);
        }
    }

    [Fact]
    public void PreservesExplicitUnknownTypesWithoutNegativeCoreMetadata()
    {
        foreach (var expectation in UnknownMappings)
        {
            var mapping = SqlServerTypeMapper.Map(expectation.Metadata);

            Assert.Equal(CanonicalScalarType.Unknown, mapping.CanonicalType);
            Assert.Equal(expectation.Name, mapping.ProviderType.Name);
            Assert.Equal(expectation.Name, mapping.ProviderType.StoreRepresentation);
            Assert.Null(mapping.ProviderType.Length);
            Assert.Null(mapping.ProviderType.Precision);
            Assert.Null(mapping.ProviderType.Scale);
        }
    }

    [Fact]
    public void NormalizesMatchingAndOutputWithOrdinalInvariantSemantics()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            var mapping = SqlServerTypeMapper.Map(new SqlServerColumnTypeMetadata("  VARCHAR  ", 16, 0, 0));

            Assert.Equal(CanonicalScalarType.String, mapping.CanonicalType);
            Assert.Equal("varchar", mapping.ProviderType.Name);
            Assert.Equal("varchar(16)", mapping.ProviderType.StoreRepresentation);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(" ", 1, 0, 0)]
    [InlineData("int", -2, 0, 0)]
    [InlineData("nchar", 3, 0, 0)]
    [InlineData("char", -1, 0, 0)]
    [InlineData("binary", 0, 0, 0)]
    [InlineData("int", 8, 10, 0)]
    [InlineData("smallmoney", 4, 19, 4)]
    [InlineData("date", 3, 1, 0)]
    [InlineData("rowversion", 8, 0, 1)]
    [InlineData("decimal", 5, 0, 0)]
    [InlineData("numeric", 17, 39, 0)]
    [InlineData("decimal", 9, 18, 19)]
    [InlineData("decimal", 9, 1, 0)]
    [InlineData("decimal", 5, 10, 0)]
    [InlineData("decimal", 9, 20, 0)]
    [InlineData("decimal", 13, 29, 0)]
    [InlineData("float", 8, 0, 0)]
    [InlineData("float", 8, 54, 0)]
    [InlineData("float", 8, 53, 1)]
    [InlineData("float", 8, 24, 0)]
    [InlineData("float", 4, 25, 0)]
    [InlineData("time", 3, 9, 0)]
    [InlineData("time", 4, 11, 3)]
    [InlineData("time", 5, 15, 7)]
    [InlineData("datetime2", 6, 20, 0)]
    [InlineData("datetime2", 7, 22, 3)]
    [InlineData("datetime2", 8, 28, 7)]
    [InlineData("datetimeoffset", 8, 27, 0)]
    [InlineData("datetimeoffset", 9, 29, 3)]
    [InlineData("datetimeoffset", 10, 35, 7)]
    [InlineData("char", 8001, 0, 0)]
    [InlineData("nvarchar", 8002, 0, 0)]
    [InlineData("varbinary", 8001, 0, 0)]
    [InlineData("varchar", -1, 1, 0)]
    [InlineData("nvarchar", -1, 0, 1)]
    [InlineData("varbinary", -1, 1, 0)]
    [InlineData("time", 5, 0, 3)]
    [InlineData("datetime2", 7, 23, 8)]
    [InlineData("datetimeoffset", 8, 26, -1)]
    public void RejectsStructurallyImpossibleKnownMetadata(string name, int maxLength, int precision, int scale)
    {
        Assert.ThrowsAny<ArgumentException>(() => SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata(name, maxLength, precision, scale)));
    }

    [Theory]
    [InlineData("time", 4, 8, 0)]
    [InlineData("time", 3, 12, 3)]
    [InlineData("time", 4, 16, 7)]
    [InlineData("datetime2", 7, 19, 0)]
    [InlineData("datetime2", 6, 23, 3)]
    [InlineData("datetime2", 7, 27, 7)]
    [InlineData("datetimeoffset", 9, 26, 0)]
    [InlineData("datetimeoffset", 8, 30, 3)]
    [InlineData("datetimeoffset", 9, 34, 7)]
    public void RejectsInvalidTemporalLengthAtEveryScaleBand(string name, int maxLength, int precision, int scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlServerTypeMapper.Map(
            new SqlServerColumnTypeMetadata(name, maxLength, precision, scale)));
    }

    private static TypeMappingExpectation Type(
        string name,
        int maxLength,
        int catalogPrecision,
        int catalogScale,
        CanonicalScalarType canonicalType,
        string storeRepresentation,
        int? length = null,
        int? precision = null,
        int? scale = null) =>
        new(
            new SqlServerColumnTypeMetadata(name, maxLength, catalogPrecision, catalogScale),
            canonicalType,
            storeRepresentation,
            length,
            precision,
            scale);

    private sealed record TypeMappingExpectation(
        SqlServerColumnTypeMetadata Metadata,
        CanonicalScalarType CanonicalType,
        string StoreRepresentation,
        int? Length,
        int? Precision,
        int? Scale);

    private sealed record UnknownMappingExpectation(
        string ProviderTypeName,
        int MaxLength,
        int Precision,
        int Scale,
        string Name)
    {
        public SqlServerColumnTypeMetadata Metadata => new(ProviderTypeName, MaxLength, Precision, Scale);
    }
}
