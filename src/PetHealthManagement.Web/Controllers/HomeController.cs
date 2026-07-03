using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Home;

namespace PetHealthManagement.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // ログイン済みユーザーの入口はマイページに一本化する（トップは未ログイン向け）
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "MyPage");
        }

        var viewModel = new HomeIndexViewModel
        {
            AvatarUrl = ImageUrlHelper.DefaultAvatarUrl,
            PetPhotoUrl = ImageUrlHelper.DefaultPetPhotoUrl
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
