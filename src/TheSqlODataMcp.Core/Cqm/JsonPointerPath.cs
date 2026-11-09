using System.Globalization;
using System.Text;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// Converts the segments of a JSON Pointer (RFC 6901) into the diagnostic path syntax used by CQM and
/// overlay errors. Segments are read from the pointer object rather than its string form: JsonSchema.Net's
/// <c>InstanceLocation.ToString()</c> can leave a decoded <c>/</c> inside a property name, which is
/// indistinguishable from a segment separator (SQL7-04).
/// </summary>
internal static class JsonPointerPath
{
    public static string ToJsonPath(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var builder = new StringBuilder("$");
        foreach (var segment in segments)
        {
            if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                builder.Append('[').Append(segment).Append(']');
            }
            else
            {
                builder.Append('.').Append(segment);
            }
        }

        return builder.ToString();
    }
}
