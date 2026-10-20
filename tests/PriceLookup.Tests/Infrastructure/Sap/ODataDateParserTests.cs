using PriceLookup.Core.Infrastructure.Sap.OData;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class ODataDateParserTests
{
    private static readonly DateTimeOffset Expected = new(2025, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Milliseconds_without_offset_parse_as_utc()
    {
        Assert.True(ODataDateParser.TryParse("/Date(1760868000000)/", out var result));

        Assert.Equal(Expected, result);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Theory]
    [InlineData("/Date(1760868000000+0000)/")]
    [InlineData("/Date(1760868000000+0200)/")]
    [InlineData("/Date(1760868000000-0530)/")]
    public void Offset_is_ignored_because_the_milliseconds_are_utc(string value)
    {
        Assert.True(ODataDateParser.TryParse(value, out var result));

        Assert.Equal(Expected, result);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void Negative_milliseconds_are_before_1970()
    {
        Assert.True(ODataDateParser.TryParse("/Date(-1000)/", out var result));

        Assert.Equal(new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("2025-10-19T10:00:00Z")]
    [InlineData("Date(1760868000000)")]
    [InlineData("/Date()/")]
    [InlineData("/Date(abc)/")]
    [InlineData("/Date(1760868000000+02)/")]
    [InlineData("/Date(1760868000000)/ ")]
    [InlineData("/Date(99999999999999999999)/")]
    [InlineData("/Date(253402300800000)/")]
    public void Garbage_returns_false_without_throwing(string? value)
    {
        Assert.False(ODataDateParser.TryParse(value, out _));
    }
}
