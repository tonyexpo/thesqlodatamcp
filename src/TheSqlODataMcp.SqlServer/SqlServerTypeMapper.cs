using System.Globalization;
using System.Text;
using TheSqlODataMcp.Core.Catalog;

namespace TheSqlODataMcp.SqlServer;

/// <summary>
/// SQL Server column type metadata read from the catalog. All values use the
/// semantics of <c>sys.columns</c>, without exposing provider client types.
/// </summary>
public sealed record SqlServerColumnTypeMetadata(
    string ProviderTypeName,
    int MaxLength,
    int Precision,
    int Scale);

/// <summary>
/// The provider-neutral and provider-specific result of SQL Server type mapping.
/// </summary>
public sealed record SqlServerTypeMapping(
    CanonicalScalarType CanonicalType,
    ProviderTypeDetails ProviderType);

/// <summary>
/// Maps SQL Server catalog type metadata to the provider-neutral catalog vocabulary.
/// </summary>
public static class SqlServerTypeMapper
{
    /// <summary>
    /// Maps one SQL Server column type using the metadata conventions of <c>sys.columns</c>.
    /// </summary>
    public static SqlServerTypeMapping Map(SqlServerColumnTypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var name = NormalizeTypeName(metadata.ProviderTypeName);
        ValidateCommonMetadata(metadata);

        return name switch
        {
            "bit" => Fixed(name, CanonicalScalarType.Boolean, metadata, 1, 1, 0),
            "tinyint" => Fixed(name, CanonicalScalarType.Int16, metadata, 1, 3, 0),
            "smallint" => Fixed(name, CanonicalScalarType.Int16, metadata, 2, 5, 0),
            "int" => Fixed(name, CanonicalScalarType.Int32, metadata, 4, 10, 0),
            "bigint" => Fixed(name, CanonicalScalarType.Int64, metadata, 8, 19, 0),
            "decimal" or "numeric" => Decimal(name, metadata),
            "money" => Fixed(name, CanonicalScalarType.Decimal, metadata, 8, 19, 4, preservePrecision: true, preserveScale: true),
            "smallmoney" => Fixed(name, CanonicalScalarType.Decimal, metadata, 4, 10, 4, preservePrecision: true, preserveScale: true),
            "real" => Fixed(name, CanonicalScalarType.Double, metadata, 4, 24, 0, preservePrecision: true),
            "float" => Float(metadata),
            "char" or "varchar" => Character(name, metadata, unicode: false),
            "nchar" or "nvarchar" => Character(name, metadata, unicode: true),
            "text" or "ntext" => DeprecatedText(name, metadata),
            "uniqueidentifier" => Fixed(name, CanonicalScalarType.Guid, metadata, 16, 0, 0),
            "date" => Fixed(name, CanonicalScalarType.Date, metadata, 3, 10, 0),
            "time" => Temporal(name, CanonicalScalarType.Time, metadata),
            "datetime" => Fixed(name, CanonicalScalarType.DateTime, metadata, 8, 23, 3),
            "smalldatetime" => Fixed(name, CanonicalScalarType.DateTime, metadata, 4, 16, 0),
            "datetime2" => Temporal(name, CanonicalScalarType.DateTime, metadata),
            "datetimeoffset" => Temporal(name, CanonicalScalarType.DateTimeOffset, metadata),
            "binary" or "varbinary" => Binary(name, metadata),
            "image" => DeprecatedBinary(name, metadata),
            "timestamp" or "rowversion" => Fixed(name, CanonicalScalarType.Binary, metadata, 8, 0, 0),
            "xml" or "sql_variant" or "hierarchyid" or "geometry" or "geography" => Unknown(name),
            _ => Unknown(name),
        };
    }

    private static SqlServerTypeMapping Fixed(
        string name,
        CanonicalScalarType canonicalType,
        SqlServerColumnTypeMetadata metadata,
        int expectedMaxLength,
        int expectedPrecision,
        int expectedScale,
        bool preservePrecision = false,
        bool preserveScale = false)
    {
        ValidateExactMetadata(metadata, name, expectedMaxLength, expectedPrecision, expectedScale);
        return new SqlServerTypeMapping(
            canonicalType,
            new ProviderTypeDetails(
                name,
                name,
                precision: preservePrecision ? expectedPrecision : null,
                scale: preserveScale ? expectedScale : null));
    }

    private static SqlServerTypeMapping Decimal(string name, SqlServerColumnTypeMetadata metadata)
    {
        if (metadata.Precision is < 1 or > 38)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Decimal precision must be between 1 and 38.");
        }

        if (metadata.Scale < 0 || metadata.Scale > metadata.Precision)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Decimal scale must be between zero and its precision.");
        }

        var expectedMaxLength = metadata.Precision switch
        {
            <= 9 => 5,
            <= 19 => 9,
            <= 28 => 13,
            _ => 17,
        };
        if (metadata.MaxLength != expectedMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Decimal storage length does not match its precision.");
        }

        return new SqlServerTypeMapping(
            CanonicalScalarType.Decimal,
            new ProviderTypeDetails(
                name,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name}({metadata.Precision},{metadata.Scale})"),
                precision: metadata.Precision,
                scale: metadata.Scale));
    }

    private static SqlServerTypeMapping Float(SqlServerColumnTypeMetadata metadata)
    {
        if (metadata.Precision is < 1 or > 53 || metadata.Scale != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Float precision must be between 1 and 53 and scale must be zero.");
        }

        var expectedMaxLength = metadata.Precision <= 24 ? 4 : 8;
        if (metadata.MaxLength != expectedMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Float storage length does not match its precision.");
        }

        return new SqlServerTypeMapping(
            CanonicalScalarType.Double,
            new ProviderTypeDetails(
                "float",
                string.Create(CultureInfo.InvariantCulture, $"float({metadata.Precision})"),
                precision: metadata.Precision));
    }

    private static SqlServerTypeMapping Character(string name, SqlServerColumnTypeMetadata metadata, bool unicode)
    {
        ValidateZeroPrecisionAndScale(metadata, name);
        var maxCapable = name is "varchar" or "nvarchar";
        if (metadata.MaxLength == -1)
        {
            if (!maxCapable)
            {
                throw new ArgumentOutOfRangeException(nameof(metadata), "Only variable character types can use the max sentinel.");
            }

            return new SqlServerTypeMapping(
                CanonicalScalarType.String,
                new ProviderTypeDetails(name, string.Concat(name, "(max)")));
        }

        if (metadata.MaxLength is < 1 or > 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Character length must be positive.");
        }

        if (unicode && metadata.MaxLength % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Unicode character length must be an even number of UTF-16 bytes.");
        }

        var length = unicode ? metadata.MaxLength / 2 : metadata.MaxLength;
        if (unicode && length > 4000)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Unicode character length cannot exceed 4,000 characters.");
        }

        return new SqlServerTypeMapping(
            CanonicalScalarType.String,
            new ProviderTypeDetails(
                name,
                string.Create(CultureInfo.InvariantCulture, $"{name}({length})"),
                length: length));
    }

    private static SqlServerTypeMapping DeprecatedText(string name, SqlServerColumnTypeMetadata metadata)
    {
        ValidatePositiveLengthAndZeroPrecisionAndScale(metadata, name);
        return new SqlServerTypeMapping(CanonicalScalarType.String, new ProviderTypeDetails(name, name));
    }

    private static SqlServerTypeMapping Temporal(
        string name,
        CanonicalScalarType canonicalType,
        SqlServerColumnTypeMetadata metadata)
    {
        if (metadata.Scale is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Temporal scale must be between zero and seven.");
        }

        var lengthOffset = metadata.Scale switch
        {
            <= 2 => 0,
            <= 4 => 1,
            _ => 2,
        };
        var (expectedMaxLength, expectedPrecision) = name switch
        {
            "time" => (3 + lengthOffset, metadata.Scale == 0 ? 8 : 9 + metadata.Scale),
            "datetime2" => (6 + lengthOffset, metadata.Scale == 0 ? 19 : 20 + metadata.Scale),
            "datetimeoffset" => (8 + lengthOffset, metadata.Scale == 0 ? 26 : 27 + metadata.Scale),
            _ => throw new InvalidOperationException("Unsupported temporal type."),
        };
        if (metadata.MaxLength != expectedMaxLength || metadata.Precision != expectedPrecision)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Temporal metadata has an invalid length or precision for its scale.");
        }

        return new SqlServerTypeMapping(
            canonicalType,
            new ProviderTypeDetails(
                name,
                string.Create(CultureInfo.InvariantCulture, $"{name}({metadata.Scale})"),
                scale: metadata.Scale));
    }

    private static SqlServerTypeMapping Binary(string name, SqlServerColumnTypeMetadata metadata)
    {
        ValidateZeroPrecisionAndScale(metadata, name);
        var maxCapable = name == "varbinary";
        if (metadata.MaxLength == -1)
        {
            if (!maxCapable)
            {
                throw new ArgumentOutOfRangeException(nameof(metadata), "Only varbinary can use the max sentinel.");
            }

            return new SqlServerTypeMapping(
                CanonicalScalarType.Binary,
                new ProviderTypeDetails(name, string.Concat(name, "(max)")));
        }

        if (metadata.MaxLength is < 1 or > 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Binary length must be positive.");
        }

        return new SqlServerTypeMapping(
            CanonicalScalarType.Binary,
            new ProviderTypeDetails(
                name,
                string.Create(CultureInfo.InvariantCulture, $"{name}({metadata.MaxLength})"),
                length: metadata.MaxLength));
    }

    private static SqlServerTypeMapping DeprecatedBinary(string name, SqlServerColumnTypeMetadata metadata)
    {
        ValidatePositiveLengthAndZeroPrecisionAndScale(metadata, name);
        return new SqlServerTypeMapping(CanonicalScalarType.Binary, new ProviderTypeDetails(name, name));
    }

    private static SqlServerTypeMapping Unknown(string name) =>
        new(CanonicalScalarType.Unknown, new ProviderTypeDetails(name, name));

    private static void ValidateCommonMetadata(SqlServerColumnTypeMetadata metadata)
    {
        if (metadata.MaxLength < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Maximum length cannot be less than the SQL Server max sentinel.");
        }

        if (metadata.Precision < 0 || metadata.Scale < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), "Precision and scale cannot be negative.");
        }
    }

    private static void ValidateExactMetadata(
        SqlServerColumnTypeMetadata metadata,
        string name,
        int expectedMaxLength,
        int expectedPrecision,
        int expectedScale)
    {
        if (metadata.MaxLength != expectedMaxLength
            || metadata.Precision != expectedPrecision
            || metadata.Scale != expectedScale)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), $"{name} has invalid SQL Server catalog metadata.");
        }
    }

    private static void ValidatePositiveLengthAndZeroPrecisionAndScale(SqlServerColumnTypeMetadata metadata, string name)
    {
        if (metadata.MaxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), $"{name} requires a positive maximum length.");
        }

        ValidateZeroPrecisionAndScale(metadata, name);
    }

    private static void ValidateZeroPrecisionAndScale(SqlServerColumnTypeMetadata metadata, string name)
    {
        if (metadata.Precision != 0 || metadata.Scale != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata), $"{name} requires zero precision and scale.");
        }
    }

    private static string NormalizeTypeName(string providerTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerTypeName);

        var normalized = new StringBuilder(providerTypeName.Length);
        var previousWasWhitespace = false;
        foreach (var character in providerTypeName.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    normalized.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            normalized.Append(char.ToLowerInvariant(character));
            previousWasWhitespace = false;
        }

        return normalized.ToString();
    }
}
