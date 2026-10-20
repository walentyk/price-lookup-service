using PriceLookup.Core.Domain;

namespace PriceLookup.Tests.Domain;

public sealed class SkuTests
{
    [Theory]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData("  abc-123  ", "ABC-123")]
    [InlineData("A", "A")]
    [InlineData("A.B_C-1", "A.B_C-1")]
    public void Valid_sku_is_parsed_and_normalised(string raw, string expected)
    {
        Assert.True(Sku.TryParse(raw, out var sku));
        Assert.Equal(expected, sku.Value);
    }

    [Fact]
    public void Forty_characters_is_valid()
    {
        var raw = new string('A', 40);

        Assert.True(Sku.TryParse(raw, out var sku));
        Assert.Equal(raw, sku.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-ABC")]
    [InlineData("AB C")]
    [InlineData("AB/C")]
    [InlineData("ÄBC")]
    [InlineData("ABC'1")]
    public void Invalid_sku_is_rejected(string? raw)
    {
        Assert.False(Sku.TryParse(raw, out var sku));
        Assert.Null(sku);
    }

    [Fact]
    public void Forty_one_characters_is_rejected()
    {
        Assert.False(Sku.TryParse(new string('A', 41), out _));
    }

    [Fact]
    public void Lower_case_sku_equals_upper_case_sku()
    {
        Assert.True(Sku.TryParse("abc-123", out var lower));
        Assert.True(Sku.TryParse("ABC-123", out var upper));

        Assert.Equal(upper, lower);
    }

    [Fact]
    public void To_string_returns_the_value()
    {
        Assert.True(Sku.TryParse("abc-123", out var sku));

        Assert.Equal("ABC-123", sku.ToString());
    }
}
