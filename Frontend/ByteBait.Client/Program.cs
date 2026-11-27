// Starts ByteBait's browser client, mounts its root component, and registers shared services.
// The API and database remain separate; this entry point only configures the WebAssembly app.
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ByteBait.Client;
using ByteBait.Client.Accounts;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// LoginExpiredHandler notices when the API answers 401 (S2.14)
builder.Services.AddScoped(sp => new HttpClient(new LoginExpiredHandler(sp)) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Who is logged in, read from GET /api/account/me (S2.14)
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<PlayerAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<PlayerAuthenticationStateProvider>());
builder.Services.AddScoped<AccountClient>();

await builder.Build().RunAsync();
