using ByteBait.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

// Validation errors use the JSON names ("zone", not "Zone"), same as the request body
builder.Services.AddControllers(options =>
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()));
builder.Services.AddOpenApi();

// All error responses are ProblemDetails (RFC 9457), see docs/api-contract.md
builder.Services.AddProblemDetails();

// Locally the connection string comes from user-secrets, in CI and Dokploy from ConnectionStrings__Default
builder.Services.AddByteBaitData(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseWebAssemblyDebugging();
}

app.UseHttpsRedirection();

// The API also serves the Blazor client (Frontend/ByteBait.Client), so each environment is one container.
// MapStaticAssets serves all of the client's files, including _framework (UseBlazorFrameworkFiles is not needed
// and conflicts with it).
app.MapStaticAssets();

app.UseAuthorization();

app.MapControllers();

// Unknown /api routes stay a 404 ProblemDetails instead of falling through to the Blazor page
app.MapFallback("/api/{**path}", () => Results.NotFound());

// Every other unknown route is a Blazor page (e.g. /game), so the client router handles it
app.MapFallbackToFile("index.html");

app.Run();

// Makes Program visible to WebApplicationFactory in ByteBait.Api.Tests
public partial class Program;
