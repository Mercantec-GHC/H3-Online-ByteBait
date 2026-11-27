namespace ByteBait.Client.Accounts;

// Where to go after login. Only pages on our own site, so a link can't send the player somewhere else.
public static class SafeReturnUrl
{
    // baseUri is NavigationManager.BaseUri. Returns an absolute address on that site, or "/".
    public static string OrHome(string? returnUrl, Uri baseUri)
    {
        // Browsers drop tabs and line breaks and read \ as /, so "/\t/evil.example" ends up as "//evil.example".
        // Our own links never contain them.
        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.Any(c => char.IsControl(c) || c == '\\'))
        {
            return "/";
        }

        // The final address decides, not the first characters: "//evil.example" and "/.//evil.example" resolve to
        // another site or to a path the browser would read as one. An absolute address on our site can't do either.
        if (!Uri.TryCreate(baseUri, returnUrl, out var target)
            || !string.Equals(target.GetLeftPart(UriPartial.Authority), baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return target.AbsoluteUri;
    }
}
