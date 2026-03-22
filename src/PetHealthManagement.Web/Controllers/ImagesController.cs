using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("images")]
public class ImagesController(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService) : Controller
{
    [HttpGet("{imageId:guid}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Get(Guid imageId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var imageAsset = await dbContext.ImageAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageId == imageId, cancellationToken);

        if (imageAsset is null || imageAsset.Status == ImageAssetStatus.Pending)
        {
            return NotFound();
        }

        if (!await IsAuthorizedToReadImageAsync(imageAsset, userId, cancellationToken))
        {
            return NotFound();
        }

        var stream = await imageStorageService.OpenReadAsync(imageAsset.StorageKey, cancellationToken);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = "inline";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(stream, imageAsset.ContentType);
    }

    private async Task<bool> IsAuthorizedToReadImageAsync(ImageAsset imageAsset, string userId, CancellationToken cancellationToken)
    {
        if (string.Equals(imageAsset.Category, "Avatar", StringComparison.Ordinal))
        {
            var owner = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AvatarImageId == imageAsset.ImageId, cancellationToken);

            return owner is not null && string.Equals(owner.Id, userId, StringComparison.Ordinal);
        }

        if (string.Equals(imageAsset.Category, "HealthLog", StringComparison.Ordinal))
        {
            var ownerId = await dbContext.HealthLogImages
                .AsNoTracking()
                .Where(x => x.ImageId == imageAsset.ImageId)
                .Select(x => x.HealthLog.Pet.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            return !string.IsNullOrEmpty(ownerId) && string.Equals(ownerId, userId, StringComparison.Ordinal);
        }

        if (string.Equals(imageAsset.Category, "Visit", StringComparison.Ordinal))
        {
            var ownerId = await dbContext.VisitImages
                .AsNoTracking()
                .Where(x => x.ImageId == imageAsset.ImageId)
                .Select(x => x.Visit.Pet.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            return !string.IsNullOrEmpty(ownerId) && string.Equals(ownerId, userId, StringComparison.Ordinal);
        }

        if (!string.Equals(imageAsset.Category, "PetPhoto", StringComparison.Ordinal))
        {
            return false;
        }

        var pet = await dbContext.Pets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PhotoImageId == imageAsset.ImageId, cancellationToken);

        if (pet is null)
        {
            return false;
        }

        return string.Equals(pet.OwnerId, userId, StringComparison.Ordinal) || pet.IsPublic;
    }
}
