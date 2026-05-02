using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Home;

namespace PetHealthManagement.Web.Controllers;

public class HomeController(ApplicationDbContext dbContext) : Controller
{
    private const string DefaultAvatarUrl = "/images/default/avatar-placeholder.webp";
    private const string DefaultPetPhotoUrl = "/images/default/pet-placeholder.webp";

    public async Task<IActionResult> Index()
    {
        var viewModel = new HomeIndexViewModel
        {
            AvatarUrl = DefaultAvatarUrl,
            PetPhotoUrl = DefaultPetPhotoUrl
        };

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userId))
        {
            return View(viewModel);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.AvatarImageId })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return View(viewModel);
        }

        var latestPetPhotoImageId = await dbContext.Pets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.PhotoImageId)
            .FirstOrDefaultAsync();

        viewModel.AvatarUrl = ResolveAvatarUrl(user.AvatarImageId);
        viewModel.PetPhotoUrl = ResolvePetPhotoUrl(latestPetPhotoImageId);

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

    private static string ResolveAvatarUrl(Guid? avatarImageId)
    {
        return avatarImageId is null ? DefaultAvatarUrl : $"/images/{avatarImageId.Value:D}";
    }

    private static string ResolvePetPhotoUrl(Guid? photoImageId)
    {
        return photoImageId is null ? DefaultPetPhotoUrl : $"/images/{photoImageId.Value:D}";
    }
}
