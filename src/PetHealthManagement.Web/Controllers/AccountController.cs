using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Account;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Account")]
public class AccountController(
    ApplicationDbContext dbContext,
    IUserAvatarService userAvatarService,
    IUserDataDeletionService userDataDeletionService,
    ILogger<AccountController> logger) : Controller
{
    private const string DefaultAvatarUrl = "/images/default/avatar-placeholder.svg";

    [HttpGet("EditProfile")]
    public async Task<IActionResult> EditProfile(string? returnUrl)
    {
        var user = await GetCurrentUserAsync(asNoTracking: true);
        if (user is null)
        {
            return Challenge();
        }

        return View(BuildEditProfileViewModel(user, UserDisplayNameHelper.ResolveForDisplay(user), returnUrl));
    }

    [HttpPost("EditProfile")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
    public async Task<IActionResult> EditProfile(EditProfileViewModel viewModel, string? returnUrl)
    {
        var user = await GetCurrentUserAsync(asNoTracking: false);
        if (user is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(BuildEditProfileViewModel(user, viewModel.DisplayName, returnUrl));
        }

        var avatarUpdateResult = await userAvatarService.ApplyAvatarChangeAsync(user, viewModel.AvatarFile, HttpContext.RequestAborted);
        if (!avatarUpdateResult.Succeeded)
        {
            ModelState.AddModelError(nameof(EditProfileViewModel.AvatarFile), avatarUpdateResult.ErrorMessage!);
            return View(BuildEditProfileViewModel(user, viewModel.DisplayName, returnUrl));
        }

        user.DisplayName = UserDisplayNameHelper.ResolveForStorage(user, viewModel.DisplayName);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return Redirect(redirectUrl);
    }

    [HttpGet("Delete")]
    public async Task<IActionResult> Delete(string? returnUrl)
    {
        var user = await GetCurrentUserAsync(asNoTracking: true);
        if (user is null)
        {
            return Challenge();
        }

        return View(BuildDeleteAccountViewModel(user, returnUrl));
    }

    [HttpPost("DeleteConfirmed")]
    public async Task<IActionResult> DeleteConfirmed(string? returnUrl)
    {
        var user = await GetCurrentUserAsync(asNoTracking: false);
        if (user is null)
        {
            return Challenge();
        }

        ApplicationOperationLogging.LogAuditStarted(
            logger,
            ApplicationOperationLogging.Operations.SelfDeleteAccount,
            user.Id,
            "User",
            user.Id);

        var deleted = await userDataDeletionService.DeleteUserAsync(user.Id, HttpContext.RequestAborted);
        if (!deleted)
        {
            ApplicationOperationLogging.LogAuditTargetNotFound(
                logger,
                ApplicationOperationLogging.Operations.SelfDeleteAccount,
                user.Id,
                "User",
                user.Id);
        }
        else
        {
            ApplicationOperationLogging.LogAuditCompleted(
                logger,
                ApplicationOperationLogging.Operations.SelfDeleteAccount,
                user.Id,
                "User",
                user.Id);
        }

        await SignOutCurrentUserAsync();

        return Redirect("/");
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync(bool asNoTracking)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var query = dbContext.Users.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == userId);
    }

    private static EditProfileViewModel BuildEditProfileViewModel(
        ApplicationUser user,
        string? displayName,
        string? returnUrl)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new EditProfileViewModel
        {
            DisplayName = displayName,
            CurrentAvatarUrl = user.AvatarImageId is null ? DefaultAvatarUrl : $"/images/{user.AvatarImageId.Value:D}",
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, "/MyPage")
        };
    }

    private static DeleteAccountViewModel BuildDeleteAccountViewModel(ApplicationUser user, string? returnUrl)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new DeleteAccountViewModel
        {
            DisplayName = UserDisplayNameHelper.ResolveForDisplay(user),
            Email = user.Email ?? string.Empty,
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, "/MyPage")
        };
    }

    private async Task SignOutCurrentUserAsync()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
    }
}
