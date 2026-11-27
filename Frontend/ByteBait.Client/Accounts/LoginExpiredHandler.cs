// Every call to the API goes through here. A 401 means the login cookie has expired (after 7 days, or a logout
// in another tab), so the page stops showing the player as logged in and [Authorize] pages send them to /login.
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Client.Accounts;

public sealed class LoginExpiredHandler(IServiceProvider services) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Looked up here and not in the constructor, because the provider itself needs the HttpClient
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            services.GetRequiredService<PlayerAuthenticationStateProvider>().LoginExpired();
        }

        return response;
    }
}
