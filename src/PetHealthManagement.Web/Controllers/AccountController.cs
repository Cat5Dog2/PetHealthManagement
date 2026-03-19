using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Account;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Account")]
public class AccountController(
    ApplicationDbContext dbContext,
    IUserAvatarService userAvatarService) : Controller
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
    public IActionResult Delete(string? returnUrl)
    {
        ViewData["ReturnUrl"] = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return View();
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
}
