using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRC.Api.JsonConverters;

public sealed class IfobtStatusJsonConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.True)
            return true;

        if (reader.TokenType == JsonTokenType.False)
            return false;

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (!reader.TryGetInt32(out var n))
                throw new JsonException("Invalid iFOBT status number.");
            return n switch
            {
                1 => true,
                0 => false,
                _ => throw new JsonException("iFOBT status must be 0 or 1.")
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = (reader.GetString() ?? string.Empty).Trim();
            if (raw.Length == 0) return null;

            var v = raw.ToUpperInvariant();
            return v switch
            {
                "1" => true,
                "0" => false,
                "TRUE" => true,
                "FALSE" => false,
                "YES" => true,
                "NO" => false,
                "Y" => true,
                "N" => false,
                "COMPLETE" => true,
                "COMPLETED" => true,
                "INCOMPLETE" => false,
                _ => throw new JsonException("Invalid iFOBT status value.")
            };
        }

        throw new JsonException("Invalid iFOBT status value.");
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteBooleanValue(value.Value);
    }
}
