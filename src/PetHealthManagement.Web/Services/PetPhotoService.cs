using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PetHealthManagement.Web.Services;

public class PetPhotoService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<PetPhotoService> logger) : IPetPhotoService
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private const long MaxUserTotalBytes = 100 * 1024 * 1024;
    private const int MaxEdgePixels = 4096;
    private const long MaxTotalPixels = 16_777_216;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<PetPhotoUpdateResult> ApplyPetPhotoChangeAsync(
        Pet pet,
        string ownerId,
        IFormFile? newPhotoFile,
        bool removePhoto,
        CancellationToken cancellationToken = default)
    {
        if (newPhotoFile is null)
        {
            if (!removePhoto)
            {
                return PetPhotoUpdateResult.Success();
            }

            await RemoveCurrentPhotoAsync(pet, cancellationToken);
            return PetPhotoUpdateResult.Success();
        }

        // Replace takes precedence when both PhotoFile and RemovePhoto are set.
        return await ReplacePhotoAsync(pet, ownerId, newPhotoFile, cancellationToken);
    }

    private async Task<PetPhotoUpdateResult> ReplacePhotoAsync(
        Pet pet,
        string ownerId,
        IFormFile newPhotoFile,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(newPhotoFile.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "PetPhoto",
                ownerId,
                "Pet",
                pet.Id,
                ImageOperationLogging.Reasons.UnsupportedExtension,
                newPhotoFile);
            return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (!AllowedContentTypes.Contains(newPhotoFile.ContentType))
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "PetPhoto",
                ownerId,
                "Pet",
                pet.Id,
                ImageOperationLogging.Reasons.UnsupportedContentType,
                newPhotoFile);
            return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (newPhotoFile.Length <= 0 || newPhotoFile.Length > MaxFileSizeBytes)
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "PetPhoto",
                ownerId,
                "Pet",
                pet.Id,
                ImageOperationLogging.Reasons.FileSizeLimitExceeded,
                newPhotoFile);
            return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        var originalTempPath = imageStorageService.CreateTemporaryPath(extension);
        var processedTempPath = imageStorageService.CreateTemporaryPath(".tmp");

        try
        {
            await imageStorageService.SaveFormFileToPathAsync(newPhotoFile, originalTempPath, cancellationToken);
            var processed = await ProcessAndValidateImageAsync(originalTempPath, processedTempPath, cancellationToken);
            if (!processed.Succeeded)
            {
                ImageOperationLogging.LogUploadRejected(
                    logger,
                    "PetPhoto",
                    ownerId,
                    "Pet",
                    pet.Id,
                    ImageOperationLogging.MapDisplayedErrorMessageToReason(processed.ErrorMessage!),
                    newPhotoFile);
                return PetPhotoUpdateResult.Fail(processed.ErrorMessage!);
            }

            var currentImageId = pet.PhotoImageId;
            var currentAsset = currentImageId is null
                ? null
                : await dbContext.ImageAssets.FirstOrDefaultAsync(x => x.ImageId == currentImageId.Value, cancellationToken);

            var userUsedBytes = await dbContext.ImageAssets
                .Where(x => x.OwnerId == ownerId && x.Status == ImageAssetStatus.Ready)
                .SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0;

            var currentImageBytes = currentAsset?.SizeBytes ?? 0;
            var projectedTotal = userUsedBytes - currentImageBytes + processed.SizeBytes;
            if (projectedTotal > MaxUserTotalBytes)
            {
                ImageOperationLogging.LogUploadRejected(
                    logger,
                    "PetPhoto",
                    ownerId,
                    "Pet",
                    pet.Id,
                    ImageOperationLogging.Reasons.UserTotalStorageLimitExceeded,
                    newPhotoFile,
                    projectedTotalBytes: projectedTotal);
                return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.TotalStorageExceeded);
            }

            var owner = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);
            if (owner is null)
            {
                ImageOperationLogging.LogOwnerNotFound(logger, "PetPhoto", ownerId, "Pet", pet.Id);
                return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
            }

            var newImageId = Guid.NewGuid();
            var storageKey = $"images/{newImageId:N}{processed.Extension}";

            var newAsset = new ImageAsset
            {
                ImageId = newImageId,
                StorageKey = storageKey,
                ContentType = processed.ContentType!,
                SizeBytes = processed.SizeBytes,
                OwnerId = ownerId,
                Category = "PetPhoto",
                Status = ImageAssetStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.ImageAssets.Add(newAsset);
            pet.PhotoImageId = newImageId;
            owner.UsedImageBytes = projectedTotal;
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
                    "PetPhoto",
                    ownerId,
                    "Pet",
                    pet.Id,
                    ImageOperationLogging.Phases.MoveToStorage,
                    storageKey);

                dbContext.ImageAssets.Remove(newAsset);
                pet.PhotoImageId = currentImageId;
                owner.UsedImageBytes = userUsedBytes;
                await dbContext.SaveChangesAsync(cancellationToken);
                return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
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
                        "PetPhoto",
                        ownerId,
                        "Pet",
                        pet.Id,
                        ImageOperationLogging.Phases.ReplaceCleanup,
                        currentAsset.ImageId,
                        currentAsset.StorageKey);
                }
            }

            return PetPhotoUpdateResult.Success();
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "PetPhoto",
                ownerId,
                "Pet",
                pet.Id,
                ImageOperationLogging.Reasons.UnsupportedImageData,
                newPhotoFile);
            return PetPhotoUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }
        finally
        {
            TryDeleteTemporaryFile(originalTempPath);
            TryDeleteTemporaryFile(processedTempPath);
        }
    }

    private async Task RemoveCurrentPhotoAsync(Pet pet, CancellationToken cancellationToken)
    {
        if (pet.PhotoImageId is null)
        {
            return;
        }

        var currentImageId = pet.PhotoImageId.Value;
        var currentAsset = await dbContext.ImageAssets
            .FirstOrDefaultAsync(x => x.ImageId == currentImageId, cancellationToken);

        var owner = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == pet.OwnerId, cancellationToken);

        pet.PhotoImageId = null;
        if (currentAsset is not null)
        {
            dbContext.ImageAssets.Remove(currentAsset);
        }

        if (owner is not null && currentAsset is not null)
        {
            owner.UsedImageBytes = Math.Max(0, owner.UsedImageBytes - currentAsset.SizeBytes);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (currentAsset is null)
        {
            return;
        }

        try
        {
            await imageStorageService.DeleteIfExistsAsync(currentAsset.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            ImageOperationLogging.LogDeleteFailed(
                logger,
                ex,
                "PetPhoto",
                pet.OwnerId,
                "Pet",
                pet.Id,
                ImageOperationLogging.Phases.RemoveCurrent,
                currentAsset.ImageId,
                currentAsset.StorageKey);
        }
    }

    private static async Task<ImageProcessingResult> ProcessAndValidateImageAsync(
        string originalTempPath,
        string processedTempPath,
        CancellationToken cancellationToken)
    {
        await using var originalStream = new FileStream(originalTempPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var detectedFormat = await Image.DetectFormatAsync(originalStream, cancellationToken);
        if (detectedFormat is null)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        var mappedFormat = MapFormat(detectedFormat);
        if (mappedFormat is null)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        var format = mappedFormat.Value;

        originalStream.Position = 0;
        using var image = await Image.LoadAsync(originalStream, cancellationToken);
        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;

        if (image.Width > MaxEdgePixels || image.Height > MaxEdgePixels)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.DimensionsExceeded);
        }

        var totalPixels = (long)image.Width * image.Height;
        if (totalPixels > MaxTotalPixels)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.DimensionsExceeded);
        }

        await using var processedStream = new FileStream(processedTempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await image.SaveAsync(processedStream, format.Encoder, cancellationToken);

        var processedSize = new FileInfo(processedTempPath).Length;
        if (processedSize <= 0)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.SaveFailed);
        }

        if (processedSize > MaxFileSizeBytes)
        {
            return ImageProcessingResult.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        return ImageProcessingResult.Success(format.Extension, format.ContentType, processedSize);
    }

    private static (string Extension, string ContentType, IImageEncoder Encoder)? MapFormat(IImageFormat detectedFormat)
    {
        if (detectedFormat.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase))
        {
            return (".jpg", "image/jpeg", new JpegEncoder { Quality = 90 });
        }

        if (detectedFormat.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase))
        {
            return (".png", "image/png", new PngEncoder());
        }

        if (detectedFormat.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase))
        {
            return (".webp", "image/webp", new WebpEncoder());
        }

        return null;
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

    private sealed record ImageProcessingResult(bool Succeeded, string? ErrorMessage, string? Extension, string? ContentType, long SizeBytes)
    {
        public static ImageProcessingResult Success(string extension, string contentType, long sizeBytes)
            => new(true, null, extension, contentType, sizeBytes);

        public static ImageProcessingResult Fail(string errorMessage)
            => new(false, errorMessage, null, null, 0);
    }
}
