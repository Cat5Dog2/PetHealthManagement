using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class VisitImageService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<VisitImageService> logger) : IVisitImageService
{
    private const int MaxImagesPerVisit = 10;
    private const string ImageCategory = "Visit";

    public async Task<VisitImageUpdateResult> ApplyImageChangesAsync(
        Visit visit,
        string ownerId,
        IReadOnlyCollection<IFormFile>? newFiles,
        IReadOnlyCollection<Guid>? deleteImageIds,
        CancellationToken cancellationToken = default)
    {
        var result = await ImageAttachmentUpdateExecutor.ApplyAsync(
            dbContext,
            imageStorageService,
            logger,
            new ImageAttachmentUpdateOptions(ImageCategory, ownerId, visit.Id, MaxImagesPerVisit),
            newFiles,
            deleteImageIds,
            LoadExistingAttachmentsAsync,
            CreateAttachment,
            LoadRegisteredAttachmentsAsync,
            cancellationToken);

        return ToVisitResult(result);
    }

    private static Task<List<VisitImage>> LoadExistingAttachmentsAsync(
        ApplicationDbContext dbContext,
        int visitId,
        CancellationToken cancellationToken)
    {
        return dbContext.VisitImages
            .Include(x => x.Image)
            .Where(x => x.VisitId == visitId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private static VisitImage CreateAttachment(int visitId, Guid imageId, int sortOrder)
    {
        return new VisitImage
        {
            VisitId = visitId,
            ImageId = imageId,
            SortOrder = sortOrder
        };
    }

    private static Task<List<VisitImage>> LoadRegisteredAttachmentsAsync(
        ApplicationDbContext dbContext,
        int visitId,
        Guid[] imageIds,
        CancellationToken cancellationToken)
    {
        return dbContext.VisitImages
            .Where(x => x.VisitId == visitId && imageIds.Contains(x.ImageId))
            .ToListAsync(cancellationToken);
    }

    private static VisitImageUpdateResult ToVisitResult(ImageAttachmentUpdateResult result)
    {
        if (result.IsConcurrencyConflict)
        {
            return VisitImageUpdateResult.ConcurrencyConflict();
        }

        return result.Succeeded
            ? VisitImageUpdateResult.Success()
            : VisitImageUpdateResult.Fail(result.ErrorMessage!);
    }
}
