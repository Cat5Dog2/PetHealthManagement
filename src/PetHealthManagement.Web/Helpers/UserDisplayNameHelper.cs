using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Helpers;

public static class UserDisplayNameHelper
{
    public static string ResolveForDisplay(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        return ResolveFallback(user);
    }

    public static string ResolveForStorage(ApplicationUser user, string? submittedDisplayName)
    {
        var normalized = submittedDisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return ResolveFallback(user);
    }

    private static string ResolveFallback(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return user.Id;
    }
}
