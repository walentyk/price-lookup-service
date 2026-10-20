using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PriceLookup.Core.Application.Resilience;
using PriceLookup.Core.DependencyInjection;

namespace PriceLookup.Tests.Application;

// Goes through AddCore, so the binding and every validation rule registered there are tested together.
public sealed class ErpResilienceOptionsTests
{
    [Fact]
    public void Missing_section_gives_valid_defaults()
    {
        var options = ResolveOptions([]);

        Assert.Equal(7000, options.TotalTimeoutMs);
        Assert.Equal(3000, options.AttemptTimeoutMs);
        Assert.Equal(3, options.RetryCount);
        Assert.Equal(200, options.RetryDelayMs);
    }

    [Theory]
    [InlineData("TotalTimeoutMs", "0")]
    [InlineData("TotalTimeoutMs", "60001")]
    [InlineData("AttemptTimeoutMs", "0")]
    [InlineData("AttemptTimeoutMs", "60001")]
    [InlineData("RetryCount", "-1")]
    [InlineData("RetryCount", "11")]
    [InlineData("RetryDelayMs", "-1")]
    [InlineData("RetryDelayMs", "10001")]
    public void Out_of_range_value_fails_validation(string name, string value)
    {
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveOptions(new() { [$"Erp:{name}"] = value }));

        Assert.Contains(name, exception.Message);
    }

    [Fact]
    public void Attempt_timeout_above_total_timeout_fails_validation()
    {
        var settings = new Dictionary<string, string?> { ["Erp:TotalTimeoutMs"] = "4000", ["Erp:AttemptTimeoutMs"] = "5000" };

        var exception = Assert.Throws<OptionsValidationException>(() => ResolveOptions(settings));

        Assert.Contains("Erp:AttemptTimeoutMs must not exceed Erp:TotalTimeoutMs", exception.Message);
    }

    private static ErpResilienceOptions ResolveOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection().AddCore(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ErpResilienceOptions>>().Value;
    }
}
