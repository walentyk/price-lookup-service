using PriceLookup.Core.Domain;

namespace PriceLookup.Tests.Domain;

public sealed class CurrencyCodeTests
{
    [Theory]
    [InlineData("EUR", "EUR")]
    [InlineData("eur", "EUR")]
    [InlineData(" usd ", "USD")]
    public void Valid_code_is_parsed_and_upper_cased(string raw, string expected)
    {
        Assert.True(CurrencyCode.TryParse(raw, out var code));
        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    public void Invalid_code_is_rejected(string? raw)
    {
        Assert.False(CurrencyCode.TryParse(raw, out var code));
        Assert.Null(code);
    }
}
