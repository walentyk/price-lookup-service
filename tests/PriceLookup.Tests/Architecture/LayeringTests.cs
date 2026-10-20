using System.Reflection;
using NetArchTest.Rules;
using PriceLookup.Core.Domain;

namespace PriceLookup.Tests.Architecture;

public sealed class LayeringTests
{
    private const string Domain = "PriceLookup.Core.Domain";
    private const string Application = "PriceLookup.Core.Application";
    private const string Infrastructure = "PriceLookup.Core.Infrastructure";
    private const string Api = "PriceLookup.Api";

    private static readonly Assembly CoreAssembly = typeof(Sku).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_application_infrastructure_or_api()
    {
        var result = Types.InAssembly(CoreAssembly)
            .That().ResideInNamespace(Domain)
            .ShouldNot().HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(CoreAssembly)
            .That().ResideInNamespace(Application)
            .ShouldNot().HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Core_does_not_depend_on_api()
    {
        var result = Types.InAssembly(CoreAssembly)
            .ShouldNot().HaveDependencyOn(Api)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Api_does_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result) =>
        Assert.True(result.IsSuccessful, $"Layer violation in: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
