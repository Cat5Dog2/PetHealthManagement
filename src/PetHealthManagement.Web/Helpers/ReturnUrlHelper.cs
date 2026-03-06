namespace PetHealthManagement.Web.Helpers;

public static class ReturnUrlHelper
{
    public static string ResolveLocalReturnUrl(string? returnUrl, string fallbackLocalUrl)
    {
        if (!IsLocalUrl(fallbackLocalUrl))
        {
            throw new ArgumentException("fallbackLocalUrl must be local URL.", nameof(fallbackLocalUrl));
        }

        return IsLocalUrl(returnUrl) ? returnUrl! : fallbackLocalUrl;
    }

    // Equivalent to ASP.NET Core Url.IsLocalUrl.
    public static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            if (url.Length == 1)
            {
                return true;
            }

            if (url[1] != '/' && url[1] != '\\')
            {
                return true;
            }

            return false;
        }

        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            return true;
        }

        return false;
    }
}
