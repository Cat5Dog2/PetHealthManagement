using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Identity.Account;

namespace PetHealthManagement.Web.Areas.Identity.Controllers;

[Area("Identity")]
[Route("Identity/Account")]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet("Login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        return View(new IdentityLoginViewModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl)
        });
    }

    [HttpPost("Login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(IdentityLoginViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl ?? returnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email.Trim(),
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            logger.LogInformation("User logged in.");
            return Redirect(ResolveReturnUrl(model.ReturnUrl));
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out.");
            ModelState.AddModelError(string.Empty, "アカウントがロックされています。時間をおいてから再度お試しください。");
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            ModelState.AddModelError(string.Empty, "二要素認証ログインは現在利用できません。");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません。");
        return View(model);
    }

    [HttpGet("Register")]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(ResolveReturnUrl(returnUrl));
        }

        return View(new IdentityRegisterViewModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl)
        });
    }

    [HttpPost("Register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(IdentityRegisterViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl = NormalizeReturnUrl(model.ReturnUrl ?? returnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email.Length <= 50 ? email : email[..50]
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            logger.LogInformation("User created a new account with password.");
            await signInManager.SignInAsync(user, isPersistent: false);
            return Redirect(ResolveReturnUrl(model.ReturnUrl));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    private static string? NormalizeReturnUrl(string? returnUrl)
    {
        return ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;
    }

    private static string ResolveReturnUrl(string? returnUrl)
    {
        return ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
    }
}
