using PriceLookup.Api.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPriceLookupProblemDetails();
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

app.Run();
