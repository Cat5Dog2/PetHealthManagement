namespace PetHealthManagement.Web.Services;

public static class ImageOperationLogging
{
    public static class Reasons
    {
        public const string UnsupportedExtension = "unsupported_extension";
        public const string UnsupportedContentType = "unsupported_content_type";
        public const string UnsupportedImageData = "unsupported_image_data";
        public const string FileSizeLimitExceeded = "file_size_limit_exceeded";
        public const string AttachmentLimitExceeded = "attachment_limit_exceeded";
        public const string UserTotalStorageLimitExceeded = "user_total_storage_limit_exceeded";
        public const string DimensionsLimitExceeded = "dimensions_limit_exceeded";
        public const string ProcessedImageWriteFailed = "processed_image_write_failed";
        public const string SaveFailed = "save_failed";
    }

    public static class Phases
    {
        public const string MoveToStorage = "move_to_storage";
        public const string SaveChanges = "save_changes";
        public const string ReplaceCleanup = "replace_cleanup";
        public const string RemoveCurrent = "remove_current";
        public const string RollbackCleanup = "rollback_cleanup";
        public const string CascadeDelete = "cascade_delete";
    }

    public static void LogUploadRejected(
        ILogger logger,
        string imageCategory,
        string ownerId,
        string resourceType,
        object resourceId,
        string reason,
        IFormFile? file = null,
        long? projectedTotalBytes = null,
        int? existingImageCount = null,
        int? requestedNewFileCount = null,
        Exception? exception = null)
    {
        if (exception is null)
        {
            logger.LogWarning(
                "Rejected image upload. imageCategory={ImageCategory} ownerId={OwnerId} resourceType={ResourceType} resourceId={ResourceId} reason={Reason} fileName={FileName} contentType={ContentType} fileSize={FileSize} existingImageCount={ExistingImageCount} requestedNewFileCount={RequestedNewFileCount} projectedTotalBytes={ProjectedTotalBytes}",
                imageCategory,
                ownerId,
                resourceType,
                resourceId,
                reason,
                file?.FileName,
                file?.ContentType,
                file?.Length,
                existingImageCount,
                requestedNewFileCount,
                projectedTotalBytes);

            return;
        }

        logger.LogWarning(
            exception,
            "Rejected image upload. imageCategory={ImageCategory} ownerId={OwnerId} resourceType={ResourceType} resourceId={ResourceId} reason={Reason} fileName={FileName} contentType={ContentType} fileSize={FileSize} existingImageCount={ExistingImageCount} requestedNewFileCount={RequestedNewFileCount} projectedTotalBytes={ProjectedTotalBytes}",
            imageCategory,
            ownerId,
            resourceType,
            resourceId,
            reason,
            file?.FileName,
            file?.ContentType,
            file?.Length,
            existingImageCount,
            requestedNewFileCount,
            projectedTotalBytes);
    }

    public static void LogOwnerNotFound(
        ILogger logger,
        string imageCategory,
        string ownerId,
        string resourceType,
        object resourceId)
    {
        logger.LogError(
            "Image owner was not found. imageCategory={ImageCategory} ownerId={OwnerId} resourceType={ResourceType} resourceId={ResourceId}",
            imageCategory,
            ownerId,
            resourceType,
            resourceId);
    }

    public static void LogPersistenceFailed(
        ILogger logger,
        Exception exception,
        string imageCategory,
        string ownerId,
        string resourceType,
        object resourceId,
        string phase,
        string? storageKey = null)
    {
        logger.LogError(
            exception,
            "Failed to persist image operation. imageCategory={ImageCategory} ownerId={OwnerId} resourceType={ResourceType} resourceId={ResourceId} phase={Phase} storageKey={StorageKey}",
            imageCategory,
            ownerId,
            resourceType,
            resourceId,
            phase,
            storageKey);
    }

    public static void LogDeleteFailed(
        ILogger logger,
        Exception exception,
        string imageCategory,
        string ownerId,
        string resourceType,
        object resourceId,
        string phase,
        Guid? imageId,
        string storageKey)
    {
        logger.LogWarning(
            exception,
            "Failed to delete image file. imageCategory={ImageCategory} ownerId={OwnerId} resourceType={ResourceType} resourceId={ResourceId} phase={Phase} imageId={ImageId} storageKey={StorageKey}",
            imageCategory,
            ownerId,
            resourceType,
            resourceId,
            phase,
            imageId,
            storageKey);
    }

    public static string MapDisplayedErrorMessageToReason(string errorMessage)
    {
        if (string.Equals(errorMessage, ImageUploadErrorMessages.UnsupportedFormat, StringComparison.Ordinal))
        {
            return Reasons.UnsupportedImageData;
        }

        if (string.Equals(errorMessage, ImageUploadErrorMessages.FileTooLarge, StringComparison.Ordinal))
        {
            return Reasons.FileSizeLimitExceeded;
        }

        if (string.Equals(errorMessage, ImageUploadErrorMessages.TooManyAttachments, StringComparison.Ordinal))
        {
            return Reasons.AttachmentLimitExceeded;
        }

        if (string.Equals(errorMessage, ImageUploadErrorMessages.TotalStorageExceeded, StringComparison.Ordinal))
        {
            return Reasons.UserTotalStorageLimitExceeded;
        }

        if (string.Equals(errorMessage, ImageUploadErrorMessages.DimensionsExceeded, StringComparison.Ordinal))
        {
            return Reasons.DimensionsLimitExceeded;
        }

        if (string.Equals(errorMessage, ImageUploadErrorMessages.SaveFailed, StringComparison.Ordinal))
        {
            return Reasons.ProcessedImageWriteFailed;
        }

        return Reasons.SaveFailed;
    }
}
