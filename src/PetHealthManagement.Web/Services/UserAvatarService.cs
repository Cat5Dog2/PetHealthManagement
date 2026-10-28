using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class UserAvatarService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<UserAvatarService> logger) : IUserAvatarService
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private const long MaxUserTotalBytes = 100 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<UserAvatarUpdateResult> ApplyAvatarChangeAsync(
        ApplicationUser user,
        IFormFile? newAvatarFile,
        CancellationToken cancellationToken = default)
    {
        if (newAvatarFile is null)
        {
            return UserAvatarUpdateResult.Success();
        }

        var extension = Path.GetExtension(newAvatarFile.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "Avatar",
                user.Id,
                "User",
                user.Id,
                ImageOperationLogging.Reasons.UnsupportedExtension,
                newAvatarFile);
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (!AllowedContentTypes.Contains(newAvatarFile.ContentType))
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "Avatar",
                user.Id,
                "User",
                user.Id,
                ImageOperationLogging.Reasons.UnsupportedContentType,
                newAvatarFile);
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (newAvatarFile.Length <= 0 || newAvatarFile.Length > MaxFileSizeBytes)
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "Avatar",
                user.Id,
                "User",
                user.Id,
                ImageOperationLogging.Reasons.FileSizeLimitExceeded,
                newAvatarFile);
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        var originalTempPath = imageStorageService.CreateTemporaryPath(extension);
        var processedTempPath = imageStorageService.CreateTemporaryPath(".tmp");

        try
        {
            await imageStorageService.SaveFormFileToPathAsync(newAvatarFile, originalTempPath, cancellationToken);
            var processed = await ImageUploadProcessor.ProcessAsync(originalTempPath, processedTempPath, cancellationToken);
            if (!processed.Succeeded)
            {
                ImageOperationLogging.LogUploadRejected(
                    logger,
                    "Avatar",
                    user.Id,
                    "User",
                    user.Id,
                    ImageOperationLogging.MapDisplayedErrorMessageToReason(processed.ErrorMessage!),
                    newAvatarFile);
                return UserAvatarUpdateResult.Fail(processed.ErrorMessage!);
            }

            var currentImageId = user.AvatarImageId;
            var currentAsset = currentImageId is null
                ? null
                : await dbContext.ImageAssets.FirstOrDefaultAsync(x => x.ImageId == currentImageId.Value, cancellationToken);

            var userUsedBytes = await dbContext.ImageAssets
                .Where(x => x.OwnerId == user.Id && x.Status == ImageAssetStatus.Ready)
                .SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0;

            var currentImageBytes = currentAsset?.SizeBytes ?? 0;
            var projectedTotal = userUsedBytes - currentImageBytes + processed.SizeBytes;
            if (projectedTotal > MaxUserTotalBytes)
            {
                ImageOperationLogging.LogUploadRejected(
                    logger,
                    "Avatar",
                    user.Id,
                    "User",
                    user.Id,
                    ImageOperationLogging.Reasons.UserTotalStorageLimitExceeded,
                    newAvatarFile,
                    projectedTotalBytes: projectedTotal);
                return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.TotalStorageExceeded);
            }

            var newImageId = Guid.NewGuid();
            var storageKey = $"images/{newImageId:N}{processed.Extension}";

            var newAsset = new ImageAsset
            {
                ImageId = newImageId,
                StorageKey = storageKey,
                ContentType = processed.ContentType!,
                SizeBytes = processed.SizeBytes,
                OwnerId = user.Id,
                Category = "Avatar",
                Status = ImageAssetStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.ImageAssets.Add(newAsset);
            user.AvatarImageId = newImageId;
            user.UsedImageBytes = projectedTotal;
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await imageStorageService.MoveToStorageAsync(processedTempPath, storageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                ImageOperationLogging.LogPersistenceFailed(
                    logger,
                    ex,
                    "Avatar",
                    user.Id,
                    "User",
                    user.Id,
                    ImageOperationLogging.Phases.MoveToStorage,
                    storageKey);

                dbContext.ImageAssets.Remove(newAsset);
                user.AvatarImageId = currentImageId;
                user.UsedImageBytes = userUsedBytes;
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
            }

            newAsset.Status = ImageAssetStatus.Ready;

            if (currentAsset is not null)
            {
                dbContext.ImageAssets.Remove(currentAsset);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (currentAsset is not null)
            {
                try
                {
                    await imageStorageService.DeleteIfExistsAsync(currentAsset.StorageKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    ImageOperationLogging.LogDeleteFailed(
                        logger,
                        ex,
                        "Avatar",
                        user.Id,
                        "User",
                        user.Id,
                        ImageOperationLogging.Phases.ReplaceCleanup,
                        currentAsset.ImageId,
                        currentAsset.StorageKey);
                }
            }

            return UserAvatarUpdateResult.Success();
        }
        finally
        {
            TryDeleteTemporaryFile(originalTempPath);
            TryDeleteTemporaryFile(processedTempPath);
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
