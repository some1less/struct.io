using System.Text.Json;
using System.Text.Json.Serialization;

namespace Struct.BLL.Core;

[JsonConverter(typeof(PurposeJsonConverter))]
public enum Purpose { Gaming, Work, Office }

// JsonStringEnumConverter also accepts numbers and "Gaming, Work"; only an exact name may bind.
public sealed class PurposeJsonConverter : JsonConverter<Purpose>
{
    public override Purpose Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (name != null && Enum.GetNames<Purpose>().Contains(name, StringComparer.OrdinalIgnoreCase))
            return Enum.Parse<Purpose>(name, ignoreCase: true);
        throw new JsonException($"Purpose must be one of: {string.Join(", ", Enum.GetNames<Purpose>())}.");
    }

    public override void Write(Utf8JsonWriter writer, Purpose value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
