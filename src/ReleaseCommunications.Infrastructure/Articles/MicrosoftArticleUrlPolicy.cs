namespace ReleaseCommunications.Infrastructure.Articles;

public static class MicrosoftArticleUrlPolicy
{
    private static readonly string[] TrustedHostSuffixes =
        ["microsoft.com"];
    private static readonly string[] TrustedExactHosts =
        ["aka.ms"];

    public static bool IsAllowed(Uri? uri)
    {
        if (uri is null ||
            !uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        return TrustedExactHosts.Contains(host, StringComparer.OrdinalIgnoreCase) ||
            TrustedHostSuffixes.Any(suffix =>
                host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase));
    }
}
