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
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes Program visible to WebApplicationFactory in ByteBait.Api.Tests
public partial class Program;
