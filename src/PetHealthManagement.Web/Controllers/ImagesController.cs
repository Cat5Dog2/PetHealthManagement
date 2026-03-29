using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("images")]
public class ImagesController(
    IOwnershipAuthorizer ownershipAuthorizer,
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

        var imageAsset = await ownershipAuthorizer.FindReadableImageAssetAsync(imageId, userId, cancellationToken);
        if (imageAsset is null)
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
}
