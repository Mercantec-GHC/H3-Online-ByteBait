// Starts ByteBait's browser client, mounts its root component, and registers shared services.
// The API and database remain separate; this entry point only configures the WebAssembly app.
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ByteBait.Client;
using ByteBait.Client.Accounts;
using ByteBait.Client.Admin;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Who is logged in, read from GET /api/account/me (S2.14)
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<PlayerAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<PlayerAuthenticationStateProvider>());
builder.Services.AddScoped<AccountClient>();

// The admin panel (S2.11). The API checks the admin role on every call.
builder.Services.AddScoped<AdminClient>();

await builder.Build().RunAsync();
