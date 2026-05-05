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

public class VisitImageService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<VisitImageService> logger) : IVisitImageService
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private const long MaxUserTotalBytes = 100 * 1024 * 1024;
    private const int MaxImagesPerVisit = 10;
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

    public async Task<VisitImageUpdateResult> ApplyImageChangesAsync(
        Visit visit,
        string ownerId,
        IReadOnlyCollection<IFormFile>? newFiles,
        IReadOnlyCollection<Guid>? deleteImageIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedNewFiles = newFiles?.Where(x => x is not null).ToList() ?? [];
        var normalizedDeleteImageIds = deleteImageIds?.Distinct().ToHashSet() ?? [];

        if (normalizedNewFiles.Count == 0 && normalizedDeleteImageIds.Count == 0)
        {
            return VisitImageUpdateResult.Success();
        }

        var owner = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);
        if (owner is null)
        {
            ImageOperationLogging.LogOwnerNotFound(logger, "Visit", ownerId, "Visit", visit.Id);
            return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
        }

        var existingImages = await dbContext.VisitImages
            .Include(x => x.Image)
            .Where(x => x.VisitId == visit.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var imagesToDelete = existingImages
            .Where(x => normalizedDeleteImageIds.Contains(x.ImageId))
            .ToList();

        var remainingImageCount = existingImages.Count - imagesToDelete.Count;
        if (remainingImageCount + normalizedNewFiles.Count > MaxImagesPerVisit)
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "Visit",
                ownerId,
                "Visit",
                visit.Id,
                ImageOperationLogging.Reasons.AttachmentLimitExceeded,
                existingImageCount: remainingImageCount,
                requestedNewFileCount: normalizedNewFiles.Count);
            return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.TooManyAttachments);
        }

        var processedUploads = new List<ProcessedUpload>();
        var registeredUploads = new List<RegisteredUpload>();
        var movedUploads = new List<RegisteredUpload>();
        var deletedAssets = imagesToDelete
            .Select(x => x.Image)
            .Where(x => x is not null)
            .ToList();

        try
        {
            foreach (var newFile in normalizedNewFiles)
            {
                var processed = await ProcessUploadAsync(newFile, cancellationToken);
                if (!processed.Succeeded)
                {
                    ImageOperationLogging.LogUploadRejected(
                        logger,
                        "Visit",
                        ownerId,
                        "Visit",
                        visit.Id,
                        ImageOperationLogging.MapDisplayedErrorMessageToReason(processed.ErrorMessage!),
                        newFile);
                    return VisitImageUpdateResult.Fail(processed.ErrorMessage!);
                }

                processedUploads.Add(processed);
            }

            var readyUserUsedBytes = await dbContext.ImageAssets
                .Where(x => x.OwnerId == ownerId && x.Status == ImageAssetStatus.Ready)
                .SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0;
            var currentUserUsedBytes = Math.Max(owner.UsedImageBytes, readyUserUsedBytes);

            var deletedBytes = deletedAssets.Sum(x => x.SizeBytes);
            var addedBytes = processedUploads.Sum(x => x.SizeBytes);
            var projectedTotal = currentUserUsedBytes - deletedBytes + addedBytes;
            if (projectedTotal > MaxUserTotalBytes)
            {
                ImageOperationLogging.LogUploadRejected(
                    logger,
                    "Visit",
                    ownerId,
                    "Visit",
                    visit.Id,
                    ImageOperationLogging.Reasons.UserTotalStorageLimitExceeded,
                    projectedTotalBytes: projectedTotal,
                    existingImageCount: remainingImageCount,
                    requestedNewFileCount: normalizedNewFiles.Count);
                return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.TotalStorageExceeded);
            }

            var nextSortOrder = existingImages
                .Where(x => !normalizedDeleteImageIds.Contains(x.ImageId))
                .Select(x => x.SortOrder)
                .DefaultIfEmpty(0)
                .Max();

            foreach (var processedUpload in processedUploads)
            {
                nextSortOrder += 1;

                var imageId = Guid.NewGuid();
                var storageKey = $"images/{imageId:N}{processedUpload.Extension}";
                var newAsset = new ImageAsset
                {
                    ImageId = imageId,
                    StorageKey = storageKey,
                    ContentType = processedUpload.ContentType!,
                    SizeBytes = processedUpload.SizeBytes,
                    OwnerId = ownerId,
                    Category = "Visit",
                    Status = ImageAssetStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                registeredUploads.Add(new RegisteredUpload(newAsset, processedUpload.ProcessedTempPath));
                dbContext.ImageAssets.Add(newAsset);

                dbContext.VisitImages.Add(new VisitImage
                {
                    VisitId = visit.Id,
                    ImageId = imageId,
                    SortOrder = nextSortOrder
                });
            }

            if (registeredUploads.Count > 0)
            {
                owner.UsedImageBytes = currentUserUsedBytes + addedBytes;

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    ImageOperationLogging.LogPersistenceFailed(
                        logger,
                        ex,
                        "Visit",
                        ownerId,
                        "Visit",
                        visit.Id,
                        ImageOperationLogging.Phases.SaveChanges);
                    dbContext.ChangeTracker.Clear();
                    return VisitImageUpdateResult.ConcurrencyConflict();
                }
                catch (Exception ex)
                {
                    ImageOperationLogging.LogPersistenceFailed(
                        logger,
                        ex,
                        "Visit",
                        ownerId,
                        "Visit",
                        visit.Id,
                        ImageOperationLogging.Phases.SaveChanges);
                    dbContext.ChangeTracker.Clear();
                    return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
                }

                foreach (var registeredUpload in registeredUploads)
                {
                    try
                    {
                        await imageStorageService.MoveToStorageAsync(
                            registeredUpload.ProcessedTempPath,
                            registeredUpload.Asset.StorageKey,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        ImageOperationLogging.LogPersistenceFailed(
                            logger,
                            ex,
                            "Visit",
                            ownerId,
                            "Visit",
                            visit.Id,
                            ImageOperationLogging.Phases.MoveToStorage,
                            registeredUpload.Asset.StorageKey);
                        dbContext.ChangeTracker.Clear();
                        await CleanupMovedFilesAsync(ownerId, visit.Id, movedUploads, cancellationToken);
                        await CleanupRegisteredUploadsAsync(
                            ownerId,
                            visit.Id,
                            registeredUploads,
                            currentUserUsedBytes,
                            cancellationToken);
                        return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
                    }

                    movedUploads.Add(registeredUpload);
                }

                foreach (var registeredUpload in registeredUploads)
                {
                    registeredUpload.Asset.Status = ImageAssetStatus.Ready;
                }
            }

            if (imagesToDelete.Count > 0)
            {
                dbContext.VisitImages.RemoveRange(imagesToDelete);
            }

            if (deletedAssets.Count > 0)
            {
                dbContext.ImageAssets.RemoveRange(deletedAssets);
            }

            owner.UsedImageBytes = projectedTotal;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                ImageOperationLogging.LogPersistenceFailed(
                    logger,
                    ex,
                    "Visit",
                    ownerId,
                    "Visit",
                    visit.Id,
                    ImageOperationLogging.Phases.SaveChanges);
                dbContext.ChangeTracker.Clear();
                await CleanupMovedFilesAsync(ownerId, visit.Id, movedUploads, cancellationToken);
                await CleanupRegisteredUploadsAsync(
                    ownerId,
                    visit.Id,
                    registeredUploads,
                    currentUserUsedBytes,
                    cancellationToken);
                return VisitImageUpdateResult.ConcurrencyConflict();
            }
            catch (Exception ex)
            {
                ImageOperationLogging.LogPersistenceFailed(
                    logger,
                    ex,
                    "Visit",
                    ownerId,
                    "Visit",
                    visit.Id,
                    ImageOperationLogging.Phases.SaveChanges);
                dbContext.ChangeTracker.Clear();
                await CleanupMovedFilesAsync(ownerId, visit.Id, movedUploads, cancellationToken);
                await CleanupRegisteredUploadsAsync(
                    ownerId,
                    visit.Id,
                    registeredUploads,
                    currentUserUsedBytes,
                    cancellationToken);
                return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.SaveFailed);
            }

            foreach (var deletedAsset in deletedAssets)
            {
                try
                {
                    await imageStorageService.DeleteIfExistsAsync(deletedAsset.StorageKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    ImageOperationLogging.LogDeleteFailed(
                        logger,
                        ex,
                        "Visit",
                        ownerId,
                        "Visit",
                        visit.Id,
                        ImageOperationLogging.Phases.ReplaceCleanup,
                        deletedAsset.ImageId,
                        deletedAsset.StorageKey);
                }
            }

            return VisitImageUpdateResult.Success();
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            ImageOperationLogging.LogUploadRejected(
                logger,
                "Visit",
                ownerId,
                "Visit",
                visit.Id,
                ImageOperationLogging.Reasons.UnsupportedImageData);
            return VisitImageUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }
        finally
        {
            foreach (var processedUpload in processedUploads)
            {
                TryDeleteTemporaryFile(processedUpload.OriginalTempPath);
                TryDeleteTemporaryFile(processedUpload.ProcessedTempPath);
            }
        }
    }

    private async Task<ProcessedUpload> ProcessUploadAsync(IFormFile newFile, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(newFile.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return ProcessedUpload.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (!AllowedContentTypes.Contains(newFile.ContentType))
        {
            return ProcessedUpload.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (newFile.Length <= 0 || newFile.Length > MaxFileSizeBytes)
        {
            return ProcessedUpload.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        var originalTempPath = imageStorageService.CreateTemporaryPath(extension);
        var processedTempPath = imageStorageService.CreateTemporaryPath(".tmp");

        try
        {
            await imageStorageService.SaveFormFileToPathAsync(newFile, originalTempPath, cancellationToken);
            var processed = await ProcessAndValidateImageAsync(originalTempPath, processedTempPath, cancellationToken);
            if (!processed.Succeeded)
            {
                TryDeleteTemporaryFile(originalTempPath);
                TryDeleteTemporaryFile(processedTempPath);
                return ProcessedUpload.Fail(processed.ErrorMessage!);
            }

            return ProcessedUpload.Success(
                originalTempPath,
                processedTempPath,
                processed.Extension!,
                processed.ContentType!,
                processed.SizeBytes);
        }
        catch
        {
            TryDeleteTemporaryFile(originalTempPath);
            TryDeleteTemporaryFile(processedTempPath);
            throw;
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

    private async Task CleanupMovedFilesAsync(
        string ownerId,
        int visitId,
        IEnumerable<RegisteredUpload> movedUploads,
        CancellationToken cancellationToken)
    {
        foreach (var movedUpload in movedUploads)
        {
            try
            {
                await imageStorageService.DeleteIfExistsAsync(movedUpload.Asset.StorageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                ImageOperationLogging.LogDeleteFailed(
                    logger,
                    ex,
                    "Visit",
                    ownerId,
                    "Visit",
                    visitId,
                    ImageOperationLogging.Phases.RollbackCleanup,
                    movedUpload.Asset.ImageId,
                    movedUpload.Asset.StorageKey);
            }
        }
    }

    private async Task CleanupRegisteredUploadsAsync(
        string ownerId,
        int visitId,
        IReadOnlyCollection<RegisteredUpload> registeredUploads,
        long previousUsedBytes,
        CancellationToken cancellationToken)
    {
        if (registeredUploads.Count == 0)
        {
            return;
        }

        try
        {
            var imageIds = registeredUploads
                .Select(x => x.Asset.ImageId)
                .ToArray();

            var registeredVisitImages = await dbContext.VisitImages
                .Where(x => x.VisitId == visitId && imageIds.Contains(x.ImageId))
                .ToListAsync(cancellationToken);
            var registeredAssets = await dbContext.ImageAssets
                .Where(x => imageIds.Contains(x.ImageId))
                .ToListAsync(cancellationToken);
            var owner = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);

            if (registeredVisitImages.Count > 0)
            {
                dbContext.VisitImages.RemoveRange(registeredVisitImages);
            }

            if (registeredAssets.Count > 0)
            {
                dbContext.ImageAssets.RemoveRange(registeredAssets);
            }

            if (owner is not null)
            {
                owner.UsedImageBytes = previousUsedBytes;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ImageOperationLogging.LogPersistenceFailed(
                logger,
                ex,
                "Visit",
                ownerId,
                "Visit",
                visitId,
                ImageOperationLogging.Phases.RollbackCleanup);
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

    private sealed record ProcessedUpload(
        bool Succeeded,
        string? ErrorMessage,
        string OriginalTempPath,
        string ProcessedTempPath,
        string? Extension,
        string? ContentType,
        long SizeBytes)
    {
        public static ProcessedUpload Success(
            string originalTempPath,
            string processedTempPath,
            string extension,
            string contentType,
            long sizeBytes)
            => new(true, null, originalTempPath, processedTempPath, extension, contentType, sizeBytes);

        public static ProcessedUpload Fail(string errorMessage)
            => new(false, errorMessage, string.Empty, string.Empty, null, null, 0);
    }

    private sealed record RegisteredUpload(ImageAsset Asset, string ProcessedTempPath);

    private sealed record ImageProcessingResult(bool Succeeded, string? ErrorMessage, string? Extension, string? ContentType, long SizeBytes)
    {
        public static ImageProcessingResult Success(string extension, string contentType, long sizeBytes)
            => new(true, null, extension, contentType, sizeBytes);

        public static ImageProcessingResult Fail(string errorMessage)
            => new(false, errorMessage, null, null, 0);
    }
}
