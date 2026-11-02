using System.Text.Json;

namespace TheSqlODataMcp.Core.Cqm;

/// <summary>
/// UTF-16 validation for JSON text and literal values. System.Text.Json accepts escaped unpaired
/// surrogates when parsing and can later throw while writing them back, so the CQM contract rejects
/// them at the boundary instead of failing during serialization.
/// </summary>
internal static class CqmUnicode
{
    public static bool TryFindInvalidUnicode(JsonElement element, string path, out string invalidPath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                try
                {
                    if (!IsValidUtf16(element.GetString()!))
                    {
                        invalidPath = path;
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    invalidPath = path;
                    return true;
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    string name;
                    try
                    {
                        name = property.Name;
                    }
                    catch (InvalidOperationException)
                    {
                        invalidPath = path;
                        return true;
                    }

                    if (!IsValidUtf16(name))
                    {
                        invalidPath = path;
                        return true;
                    }

                    if (TryFindInvalidUnicode(property.Value, string.Concat(path, ".", name), out invalidPath))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindInvalidUnicode(item, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{path}[{index}]"), out invalidPath))
                    {
                        return true;
                    }

                    index++;
                }

                break;
        }

        invalidPath = string.Empty;
        return false;
    }

    public static bool IsValidUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        return true;
    }
}
