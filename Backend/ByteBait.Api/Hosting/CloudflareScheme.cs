using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;

namespace ByteBait.Api.Hosting;

// On Dokploy, Cloudflare handles HTTPS and passes the request on as plain HTTP, so Traefik's X-Forwarded-Proto
// says "http" even when the player used https. Cloudflare's CF-Visitor header ({"scheme":"https"}) tells the truth.
public static class CloudflareScheme
{
    public const string HeaderName = "CF-Visitor";

    // After UseForwardedHeaders, so it has the last word.
    // Only a header that says http is redirected. Without the header we can't tell, and since Cloudflare talks http
    // to us, a redirect would come straight back as http again and loop forever.
    public static IApplicationBuilder UseCloudflareScheme(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var request = context.Request;
            switch (SchemeFrom(request.Headers[HeaderName]))
            {
                case "https":
                    request.Scheme = "https";
                    break;
                case "http":
                    context.Response.Redirect(
                        UriHelper.BuildAbsolute("https", request.Host, request.PathBase, request.Path, request.QueryString),
                        permanent: false,
                        preserveMethod: true);
                    return Task.CompletedTask;
            }

            return next(context);
        });

    // "https", "http" or null when the header is missing or not what Cloudflare sends.
    // Anyone can send the header, but it only changes how their own request is treated, so that's fine.
    public static string? SchemeFrom(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return null;
        }

        try
        {
            using var visitor = JsonDocument.Parse(headerValue);
            return visitor.RootElement.ValueKind == JsonValueKind.Object
                && visitor.RootElement.TryGetProperty("scheme", out var scheme)
                && scheme.ValueKind == JsonValueKind.String
                && scheme.GetString() is "https" or "http"
                ? scheme.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
