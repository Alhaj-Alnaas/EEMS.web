using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace PMS.web.Navigation
{
    /// <summary>
    /// Carries the list the user came from, and only accepts in-app paths.
    /// </summary>
    public static class ReturnUrl
    {
        public static string Go(NavigationManager navigation, string target)
        {
            var here = Current(navigation);
            var herePath = here.Split('?')[0];
            var targetPath = target.Split('?')[0];
            if (string.Equals(herePath, targetPath, StringComparison.OrdinalIgnoreCase))
                return here;

            var separator = target.Contains('?') ? "&" : "?";
            return target + separator + "from=" + Uri.EscapeDataString(here);
        }

        public static string? Read(NavigationManager navigation)
        {
            var uri = navigation.ToAbsoluteUri(navigation.Uri);
            if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("from", out var values))
                return null;

            return IsLocal(values.ToString()) ? values.ToString() : null;
        }

        public static string Current(NavigationManager navigation)
        {
            var relative = navigation.ToBaseRelativePath(navigation.Uri);
            if (string.IsNullOrEmpty(relative))
                return "/";
            return relative.StartsWith('/') ? relative : "/" + relative;
        }

        public static bool IsLocal(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            if (!url.StartsWith('/') || url.StartsWith("//") || url.StartsWith("/\\"))
                return false;
            return !url.Contains("://", StringComparison.Ordinal) && !url.Contains('\\');
        }
    }
}
