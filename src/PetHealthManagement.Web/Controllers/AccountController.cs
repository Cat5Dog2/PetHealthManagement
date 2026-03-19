using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Helpers;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Account")]
public class AccountController : Controller
{
    [HttpGet("EditProfile")]
    public IActionResult EditProfile(string? returnUrl)
    {
        ViewData["ReturnUrl"] = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return View();
    }

    [HttpGet("Delete")]
    public IActionResult Delete(string? returnUrl)
    {
        ViewData["ReturnUrl"] = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return View();
    }
}
