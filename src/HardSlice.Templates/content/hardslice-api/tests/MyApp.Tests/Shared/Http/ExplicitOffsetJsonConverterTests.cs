using System.Text.Json;
using MyApp.Shared.Http;

namespace MyApp.Tests.Shared.Http;

public sealed class ExplicitOffsetJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new ExplicitOffsetJsonConverter() } };

    [Theory]
    [InlineData("\"2100-01-04T09:00:00Z\"", 0)]
    [InlineData("\"2100-01-04T10:00:00+01:00\"", 1)]
    [InlineData("\"2100-01-04T02:00:00.1234567-07:00\"", -7)]
    public void Reads_a_date_with_its_offset_as_the_same_instant(string json, int offsetHours)
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>(json, Options);

        value.Offset.ShouldBe(TimeSpan.FromHours(offsetHours));
        value.UtcDateTime.Date.ShouldBe(new DateTime(2100, 1, 4));
    }

    [Theory]
    [InlineData("\"2100-01-04T09:00:00\"")]
    [InlineData("\"2100-01-04\"")]
    [InlineData("\"2100-01-04T09:00:00+0100\"")]
    [InlineData("\"not a date Z\"")]
    [InlineData("4102736400")]
    public void Refuses_a_date_without_offset_or_that_is_no_ISO_8601_string(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>(json, Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset?>(json, Options));
    }

    [Fact]
    public void Reads_null_as_a_missing_optional_date()
    {
        JsonSerializer.Deserialize<DateTimeOffset?>("null", Options).ShouldBeNull();
    }

    [Fact]
    public void Writes_the_date_as_System_Text_Json_does()
    {
        var value = new DateTimeOffset(2100, 1, 4, 10, 0, 0, TimeSpan.FromHours(1));

        JsonSerializer.Serialize(value, Options).ShouldBe(JsonSerializer.Serialize(value, JsonSerializerOptions.Web));
    }
}
