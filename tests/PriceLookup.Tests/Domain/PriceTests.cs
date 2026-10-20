using PriceLookup.Core.Domain;

namespace PriceLookup.Tests.Domain;

public sealed class PriceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(19.99)]
    public void Zero_or_positive_amount_is_valid(decimal amount)
    {
        var eur = Eur();

        Assert.True(Price.TryCreate(amount, eur, out var price));
        Assert.Equal(amount, price.Amount);
        Assert.Equal(eur, price.Currency);
    }

    [Fact]
    public void Negative_amount_is_rejected()
    {
        Assert.False(Price.TryCreate(-0.01m, Eur(), out var price));
        Assert.Null(price);
    }

    private static CurrencyCode Eur()
    {
        Assert.True(CurrencyCode.TryParse("EUR", out var code));
        return code;
    }
}
