using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.MyPage;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("MyPage")]
public class MyPageController(ApplicationDbContext dbContext) : Controller
{
    private const string DefaultAvatarUrl = "/images/default/avatar-placeholder.svg";
    private const string DefaultPetPhotoUrl = "/images/default/pet-placeholder.svg";

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
        {
            return Challenge();
        }

        var pets = await dbContext.Pets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new MyPetSummaryViewModel
            {
                PetId = x.Id,
                Name = x.Name,
                SpeciesLabel = SpeciesCatalog.ToLabel(x.SpeciesCode),
                PhotoUrl = ResolvePetPhotoUrl(x.PhotoImageId),
                IsPublic = x.IsPublic
            })
            .ToListAsync();

        var viewModel = new MyPageViewModel
        {
            DisplayName = UserDisplayNameHelper.ResolveForDisplay(user),
            Email = string.IsNullOrWhiteSpace(user.Email) ? "未設定" : user.Email,
            AvatarUrl = ResolveAvatarUrl(user.AvatarImageId),
            Pets = pets
        };

        return View(viewModel);
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
