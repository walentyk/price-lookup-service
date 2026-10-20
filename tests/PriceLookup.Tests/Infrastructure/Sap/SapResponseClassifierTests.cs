using System.Net;
using PriceLookup.Core.Application.Ports;
using PriceLookup.Core.Infrastructure.Sap;

namespace PriceLookup.Tests.Infrastructure.Sap;

public sealed class SapResponseClassifierTests
{
    [Fact]
    public void Status_200_carries_a_price()
    {
        Assert.Equal(SapResponseOutcome.Price, SapResponseClassifier.Classify(HttpStatusCode.OK));
    }

    [Fact]
    public void Status_404_means_not_found()
    {
        Assert.Equal(SapResponseOutcome.NotFound, SapResponseClassifier.Classify(HttpStatusCode.NotFound));
    }

    [Theory]
    [InlineData(401, ErpFailureKind.Unauthorized)]
    [InlineData(403, ErpFailureKind.Unauthorized)]
    [InlineData(408, ErpFailureKind.Transient)]
    [InlineData(429, ErpFailureKind.Transient)]
    [InlineData(500, ErpFailureKind.Transient)]
    [InlineData(502, ErpFailureKind.Transient)]
    [InlineData(503, ErpFailureKind.Transient)]
    [InlineData(504, ErpFailureKind.Transient)]
    [InlineData(599, ErpFailureKind.Transient)]
    [InlineData(201, ErpFailureKind.InvalidResponse)]
    [InlineData(204, ErpFailureKind.InvalidResponse)]
    [InlineData(302, ErpFailureKind.InvalidResponse)]
    [InlineData(400, ErpFailureKind.InvalidResponse)]
    [InlineData(405, ErpFailureKind.InvalidResponse)]
    [InlineData(409, ErpFailureKind.InvalidResponse)]
    public void Other_status_throws_the_matching_failure(int status, ErpFailureKind expected)
    {
        var exception = Assert.Throws<ErpException>(() => SapResponseClassifier.Classify((HttpStatusCode)status));

        Assert.Equal(expected, exception.Kind);
    }
}
