using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ailos.EncryptedId.JsonConverters;

public class EncryptedIdJsonConverter : JsonConverter<EncryptedId>
{
    public override EncryptedId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value) 
            ? throw new JsonException("EncryptedId não pode ser nulo ou vazio.")
            : new EncryptedId(value);
    }

    public override void Write(Utf8JsonWriter writer, EncryptedId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
