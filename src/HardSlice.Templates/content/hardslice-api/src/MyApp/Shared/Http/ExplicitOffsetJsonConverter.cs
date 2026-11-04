using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApp.Shared.Http;

// Reads a DateTimeOffset of a request only when its JSON string states its offset, 'Z' or '+01:00' for example. Without one,
// System.Text.Json would take the offset of the server, and the same request would store another instant on another machine.
// The request is then answered with 400, before any handler runs. Registered in Program.cs; writes the default ISO 8601 form.
internal sealed class ExplicitOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && reader.GetString() is { } text
            && EndsWithOffset(text)
            && reader.TryGetDateTimeOffset(out var value))
            return value;

        throw new JsonException("A date and time must be an ISO 8601 string with its offset, such as 2026-12-01T09:00:00Z or 2026-12-01T10:00:00+01:00.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    // 'Z', or '+hh:mm' and '-hh:mm': the forms of ISO 8601 that System.Text.Json reads.
    private static bool EndsWithOffset(string text) =>
        text.EndsWith('Z') || text.EndsWith('z')
        || (text.Length >= 6
            && text[^6] is '+' or '-'
            && char.IsAsciiDigit(text[^5]) && char.IsAsciiDigit(text[^4])
            && text[^3] == ':'
            && char.IsAsciiDigit(text[^2]) && char.IsAsciiDigit(text[^1]));
}
