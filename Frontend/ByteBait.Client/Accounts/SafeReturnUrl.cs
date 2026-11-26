namespace ByteBait.Client.Accounts;

// Where to go after login. Only pages on our own site, so a link can't send the player somewhere else.
public static class SafeReturnUrl
{
    public static string OrHome(string? returnUrl) =>
        returnUrl is ['/', not '/' and not '\\', ..] or "/" ? returnUrl : "/";
}
