// PURPOSE: copied verbatim from the real ag-kit Ag.Util - the real
// Agent/Office/Company rows store their public API shape as a RawJson
// column (PascalCase-ish, mixed casing from years of schema evolution).
// This walks the parsed JSON tree and rewrites every property name to
// camelCase, matching what ASP.NET Core's own serializer would produce.
namespace Ag.Util.Json;

using System.Text.Json;

public static class CamelCaseConversion
{
    public static JsonElement ConvertToCamelCase(string json)
    {
        using var document = JsonDocument.Parse(json);

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCamelCaseElement(writer, document.RootElement);
            writer.Flush();
        }

        return JsonDocument.Parse(stream.ToArray())
            .RootElement
            .Clone();
    }

    private static void WriteCamelCaseElement(
        Utf8JsonWriter writer,
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:

                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject())
                {
                    var propertyName = ConvertPropertyNameToCamelCase(property.Name);

                    writer.WritePropertyName(propertyName);

                    WriteCamelCaseElement(writer, property.Value);
                }

                writer.WriteEndObject();

                break;

            case JsonValueKind.Array:

                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteCamelCaseElement(writer, item);
                }

                writer.WriteEndArray();

                break;

            default:

                element.WriteTo(writer);

                break;
        }
    }

    private static string ConvertPropertyNameToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        if (value.All(char.IsUpper))
            return value.ToLowerInvariant();

        int upperCount = 0;

        while (upperCount < value.Length &&
               char.IsUpper(value[upperCount]))
        {
            upperCount++;
        }

        if (upperCount > 1 && upperCount < value.Length)
        {
            return value.Substring(0, upperCount - 1).ToLowerInvariant()
                 + value.Substring(upperCount - 1);
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
