using ByteBait.Api.Diagnostics;
using ByteBait.Api.Game;
using ByteBait.Api.Hosting;
using ByteBait.Api.Players;
using ByteBait.Api.Weather;
using ByteBait.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

// Locally the connection string comes from user-secrets, in CI and Dokploy from ConnectionStrings__Default.
// First, so the migrations have run before the accounts read the cookie keys from the database.
builder.Services.AddByteBaitData(builder.Configuration);

// Accounts, login cookie and roles (S2.13)
builder.Services.AddPlayerAccounts();

builder.Services.AddControllers(options =>
{
    // Validation errors use the JSON names ("zone", not "Zone"), same as the request body
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());

    // Every POST, PUT, PATCH and DELETE needs the antiforgery token, also register and login (S2.13)
    options.Filters.Add<AntiforgeryFilter>();
});
builder.Services.AddOpenApi();

// All error responses are ProblemDetails (RFC 9457), see docs/api-contract.md
builder.Services.AddProblemDetails();

// Weather from Open-Meteo, cached per town (S2.9). The timeout keeps a slow Open-Meteo from holding up the game.
builder.Services.AddHttpClient(OpenMeteoClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<OpenMeteoClient>();
builder.Services.AddSingleton<WeatherService>();

// Picks the fish for a cast on the server (S2.8). Random.Shared is safe to use from several requests at once.
builder.Services.AddSingleton(new FishPicker(Random.Shared));
builder.Services.AddSingleton(TimeProvider.System);

// On Dokploy, Traefik passes the request on to the container. The X-Forwarded headers tell the API
// the client IP and the scheme Traefik saw (CloudflareScheme corrects the scheme afterwards).
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

// Cloudflare tells us when the player used https, and http through Cloudflare is sent on to https (see CloudflareScheme).
// UseHttpsRedirection below only works locally: the container has no https port, so it does nothing there.
app.UseCloudflareScheme();

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

// Before anything that sets cookies, so every cookie gets the rules from AddPlayerAccounts (S2.15)
app.UseCookiePolicy();

app.UseAuthentication();
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
