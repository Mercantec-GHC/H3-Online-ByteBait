using System.Text.Json;

namespace ByteBait.Api.Hosting;

// On Dokploy, Cloudflare handles HTTPS and passes the request on as plain HTTP, so Traefik's X-Forwarded-Proto
// says "http" even when the player used https. Cloudflare's CF-Visitor header ({"scheme":"https"}) tells the truth.
// A request without the header keeps its scheme, so plain http is still sent on to https by UseHttpsRedirection.
public static class CloudflareScheme
{
    public const string HeaderName = "CF-Visitor";

    // After UseForwardedHeaders, so it has the last word
    public static IApplicationBuilder UseCloudflareScheme(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (IsHttps(context.Request.Headers[HeaderName]))
            {
                context.Request.Scheme = "https";
            }

            return next(context);
        });

    // Anyone can send the header, but it only changes how their own request is treated, so that's fine
    public static bool IsHttps(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return false;
        }

        try
        {
            using var visitor = JsonDocument.Parse(headerValue);
            return visitor.RootElement.ValueKind == JsonValueKind.Object
                && visitor.RootElement.TryGetProperty("scheme", out var scheme)
                && scheme.ValueEquals("https");
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
