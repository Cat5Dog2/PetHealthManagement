using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class HealthLogImageService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<HealthLogImageService> logger) : IHealthLogImageService
{
    private const int MaxImagesPerLog = 10;
    private const string ImageCategory = "HealthLog";

    public async Task<HealthLogImageUpdateResult> ApplyImageChangesAsync(
        HealthLog healthLog,
        string ownerId,
        IReadOnlyCollection<IFormFile>? newFiles,
        IReadOnlyCollection<Guid>? deleteImageIds,
        CancellationToken cancellationToken = default)
    {
        var result = await ImageAttachmentUpdateExecutor.ApplyAsync(
            dbContext,
            imageStorageService,
            logger,
            new ImageAttachmentUpdateOptions(ImageCategory, ownerId, healthLog.Id, MaxImagesPerLog),
            newFiles,
            deleteImageIds,
            LoadExistingAttachmentsAsync,
            CreateAttachment,
            LoadRegisteredAttachmentsAsync,
            cancellationToken);

        return ToHealthLogResult(result);
    }

    private static Task<List<HealthLogImage>> LoadExistingAttachmentsAsync(
        ApplicationDbContext dbContext,
        int healthLogId,
        CancellationToken cancellationToken)
    {
        return dbContext.HealthLogImages
            .Include(x => x.Image)
            .Where(x => x.HealthLogId == healthLogId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private static HealthLogImage CreateAttachment(int healthLogId, Guid imageId, int sortOrder)
    {
        return new HealthLogImage
        {
            HealthLogId = healthLogId,
            ImageId = imageId,
            SortOrder = sortOrder
        };
    }

    private static Task<List<HealthLogImage>> LoadRegisteredAttachmentsAsync(
        ApplicationDbContext dbContext,
        int healthLogId,
        Guid[] imageIds,
        CancellationToken cancellationToken)
    {
        return dbContext.HealthLogImages
            .Where(x => x.HealthLogId == healthLogId && imageIds.Contains(x.ImageId))
            .ToListAsync(cancellationToken);
    }

    private static HealthLogImageUpdateResult ToHealthLogResult(ImageAttachmentUpdateResult result)
    {
        if (result.IsConcurrencyConflict)
        {
            return HealthLogImageUpdateResult.ConcurrencyConflict();
        }

        return result.Succeeded
            ? HealthLogImageUpdateResult.Success()
            : HealthLogImageUpdateResult.Fail(result.ErrorMessage!);
    }
}
