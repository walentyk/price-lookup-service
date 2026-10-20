using PriceLookup.Api.Configuration;
using PriceLookup.Api.Endpoints;
using PriceLookup.Api.Errors;
using PriceLookup.Core.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Added after the default sources, so Key Vault values override appsettings.json (A22).
builder.Configuration.AddKeyVaultWhenConfigured();

builder.Services.AddPriceLookupProblemDetails();
builder.Services.AddCore(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// MapHealthChecks accepts every method; restricting it makes routing answer other methods with 405.
app.MapHealthChecks("/health")
    .WithMetadata(new HttpMethodMetadata([HttpMethods.Get, HttpMethods.Head]));

app.MapPriceEndpoints();

app.Run();
