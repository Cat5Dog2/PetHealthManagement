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

public class UserAvatarService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<UserAvatarService> logger) : IUserAvatarService
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
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (!AllowedContentTypes.Contains(newAvatarFile.ContentType))
        {
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        if (newAvatarFile.Length <= 0 || newAvatarFile.Length > MaxFileSizeBytes)
        {
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        var originalTempPath = imageStorageService.CreateTemporaryPath(extension);
        var processedTempPath = imageStorageService.CreateTemporaryPath(".tmp");

        try
        {
            await imageStorageService.SaveFormFileToPathAsync(newAvatarFile, originalTempPath, cancellationToken);
            var processed = await ProcessAndValidateImageAsync(originalTempPath, processedTempPath, cancellationToken);
            if (!processed.Succeeded)
            {
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
                logger.LogError(ex, "Failed to move avatar to storage. storageKey={StorageKey}", storageKey);

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
                    logger.LogWarning(ex, "Failed to delete old avatar file. storageKey={StorageKey}", currentAsset.StorageKey);
                }
            }

            return UserAvatarUpdateResult.Success();
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            return UserAvatarUpdateResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }
        finally
        {
            TryDeleteTemporaryFile(originalTempPath);
            TryDeleteTemporaryFile(processedTempPath);
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
