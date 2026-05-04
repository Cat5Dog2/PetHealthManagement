using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Identity.Manage;

namespace PetHealthManagement.Web.Areas.Identity.Controllers;

[Area("Identity")]
[Authorize]
[Route("Identity/Account/Manage")]
public class ManageController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        return View(new IdentityManageProfileViewModel
        {
            StatusMessage = ReadStatusMessage(),
            Username = await userManager.GetUserNameAsync(user) ?? string.Empty,
            PhoneNumber = await userManager.GetPhoneNumberAsync(user)
        });
    }

    [HttpPost("")]
    [HttpPost("Index")]
    public async Task<IActionResult> Index(IdentityManageProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Username = await userManager.GetUserNameAsync(user) ?? string.Empty;
            return View(model);
        }

        var phoneNumber = await userManager.GetPhoneNumberAsync(user);
        if (!string.Equals(model.PhoneNumber, phoneNumber, StringComparison.Ordinal))
        {
            var setPhoneResult = await userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
            if (!setPhoneResult.Succeeded)
            {
                TempData["StatusMessage"] = "Error: 電話番号を更新できませんでした。";
                return RedirectToAction(nameof(Index));
            }
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "プロフィールを更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Email")]
    public async Task<IActionResult> Email()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        var email = await userManager.GetEmailAsync(user) ?? string.Empty;
        return View(new IdentityManageEmailViewModel
        {
            StatusMessage = ReadStatusMessage(),
            Email = email,
            NewEmail = email,
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user)
        });
    }

    [HttpPost("Email")]
    public async Task<IActionResult> Email(IdentityManageEmailViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        var email = await userManager.GetEmailAsync(user) ?? string.Empty;
        if (!ModelState.IsValid)
        {
            model.Email = email;
            model.IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
            return View(model);
        }

        var newEmail = model.NewEmail.Trim();
        if (string.Equals(newEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            TempData["StatusMessage"] = "メールアドレスは変更されていません。";
            return RedirectToAction(nameof(Email));
        }

        var setEmailResult = await userManager.SetEmailAsync(user, newEmail);
        if (!setEmailResult.Succeeded)
        {
            AddIdentityErrors(setEmailResult);
            model.Email = email;
            model.IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
            return View(model);
        }

        var setUserNameResult = await userManager.SetUserNameAsync(user, newEmail);
        if (!setUserNameResult.Succeeded)
        {
            AddIdentityErrors(setUserNameResult);
            model.Email = email;
            model.IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
            return View(model);
        }

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            model.Email = email;
            model.IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "メールアドレスを更新しました。";
        return RedirectToAction(nameof(Email));
    }

    [HttpGet("ChangePassword")]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        return View(new IdentityManageChangePasswordViewModel
        {
            StatusMessage = ReadStatusMessage()
        });
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword(IdentityManageChangePasswordViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(
            user,
            model.OldPassword,
            model.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            AddIdentityErrors(changePasswordResult);
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = "パスワードを変更しました。";
        return RedirectToAction(nameof(ChangePassword));
    }

    [HttpGet("TwoFactorAuthentication")]
    public async Task<IActionResult> TwoFactorAuthentication()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        return View(new IdentityManageTwoFactorViewModel
        {
            IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
            RecoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user)
        });
    }

    [HttpGet("PersonalData")]
    public async Task<IActionResult> PersonalData()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? NotFound() : View();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private string? ReadStatusMessage()
    {
        return TempData.TryGetValue("StatusMessage", out var message)
            ? message as string
            : null;
    }
}
