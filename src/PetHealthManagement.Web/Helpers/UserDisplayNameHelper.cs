using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Helpers;

public static class UserDisplayNameHelper
{
    public static string ResolveForDisplay(ApplicationUser user)
    {
        return ResolveForDisplay(user.DisplayName, user.UserName, user.Email, user.Id);
    }

    public static string ResolveForDisplay(string? displayName, string? userName, string? email, string userId)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return userId;
    }

    public static string ResolveForStorage(ApplicationUser user, string? submittedDisplayName)
    {
        var normalized = submittedDisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

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
