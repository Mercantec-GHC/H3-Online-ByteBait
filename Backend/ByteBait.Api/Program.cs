using ByteBait.Api.Diagnostics;
using ByteBait.Api.Players;
using ByteBait.Api.Weather;
using ByteBait.Data;
using Microsoft.AspNetCore.HttpOverrides;
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

// Weather from Open-Meteo, cached per town (S2.9). The timeout keeps a slow Open-Meteo from holding up the game.
builder.Services.AddHttpClient(OpenMeteoClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<OpenMeteoClient>();
builder.Services.AddSingleton<WeatherService>();

// The logged-in player for the game endpoints. Always null until player accounts (S2.13, #76).
builder.Services.AddScoped<CurrentPlayer>();
builder.Services.AddSingleton(TimeProvider.System);

// On Dokploy, Traefik handles HTTPS and passes the request on to the container as plain HTTP.
// The X-Forwarded headers tell the API the original scheme and client IP, so HTTPS redirection
// and Secure cookies see the request as HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Traefik's address inside Docker isn't fixed, and the container can only be reached through it
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// First, so everything after it sees the original scheme
app.UseForwardedHeaders();

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

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    WifiThrowProbe.MapRoutes(app);
}

// Unknown /api routes stay a 404 ProblemDetails instead of falling through to the Blazor page
app.MapFallback("/api/{**path}", () => Results.NotFound());

// Every other unknown route is a Blazor page (e.g. /game), so the client router handles it
app.MapFallbackToFile("index.html");

app.Run();

// Makes Program visible to WebApplicationFactory in ByteBait.Api.Tests
public partial class Program;
