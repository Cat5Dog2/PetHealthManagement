namespace PetHealthManagement.Web.Services;

public static class ApplicationOperationLogging
{
    public static class Operations
    {
        public const string SelfDeleteAccount = "self_delete_account";
        public const string AdminDeleteUser = "admin_delete_user";
        public const string DeleteUserData = "delete_user_data";
        public const string DeletePet = "delete_pet";
        public const string DeleteHealthLog = "delete_health_log";
        public const string DeleteVisit = "delete_visit";
    }

    public static void LogAuditStarted(
        ILogger logger,
        string operation,
        string actorUserId,
        string targetType,
        object targetId)
    {
        logger.LogInformation(
            "Started audited operation. operation={Operation} actorUserId={ActorUserId} targetType={TargetType} targetId={TargetId}",
            operation,
            actorUserId,
            targetType,
            targetId);
    }

    public static void LogAuditCompleted(
        ILogger logger,
        string operation,
        string actorUserId,
        string targetType,
        object targetId)
    {
        logger.LogInformation(
            "Completed audited operation. operation={Operation} actorUserId={ActorUserId} targetType={TargetType} targetId={TargetId}",
            operation,
            actorUserId,
            targetType,
            targetId);
    }

    public static void LogAuditTargetNotFound(
        ILogger logger,
        string operation,
        string actorUserId,
        string targetType,
        object targetId)
    {
        logger.LogWarning(
            "Audited operation target was not found. operation={Operation} actorUserId={ActorUserId} targetType={TargetType} targetId={TargetId}",
            operation,
            actorUserId,
            targetType,
            targetId);
    }

    public static void LogDeletionTargetNotFound(
        ILogger logger,
        string operation,
        string ownerId,
        string targetType,
        object targetId)
    {
        logger.LogWarning(
            "Deletion target was not found. operation={Operation} ownerId={OwnerId} targetType={TargetType} targetId={TargetId}",
            operation,
            ownerId,
            targetType,
            targetId);
    }

    public static void LogDeletionPreconditionFailed(
        ILogger logger,
        string operation,
        string ownerId,
        string targetType,
        object targetId,
        string reason)
    {
        logger.LogError(
            "Deletion precondition failed. operation={Operation} ownerId={OwnerId} targetType={TargetType} targetId={TargetId} reason={Reason}",
            operation,
            ownerId,
            targetType,
            targetId,
            reason);
    }

    public static void LogDeletionStarted(
        ILogger logger,
        string operation,
        string ownerId,
        string targetType,
        object targetId,
        int petCount = 0,
        int healthLogCount = 0,
        int visitCount = 0,
        int scheduleItemCount = 0,
        int imageAssetCount = 0,
        int storageTargetCount = 0,
        long deletedReadyBytes = 0)
    {
        logger.LogInformation(
            "Started deletion operation. operation={Operation} ownerId={OwnerId} targetType={TargetType} targetId={TargetId} petCount={PetCount} healthLogCount={HealthLogCount} visitCount={VisitCount} scheduleItemCount={ScheduleItemCount} imageAssetCount={ImageAssetCount} storageTargetCount={StorageTargetCount} deletedReadyBytes={DeletedReadyBytes}",
            operation,
            ownerId,
            targetType,
            targetId,
            petCount,
            healthLogCount,
            visitCount,
            scheduleItemCount,
            imageAssetCount,
            storageTargetCount,
            deletedReadyBytes);
    }

    public static void LogDeletionCompleted(
        ILogger logger,
        string operation,
        string ownerId,
        string targetType,
        object targetId,
        int petCount = 0,
        int healthLogCount = 0,
        int visitCount = 0,
        int scheduleItemCount = 0,
        int imageAssetCount = 0,
        int storageTargetCount = 0,
        long deletedReadyBytes = 0)
    {
        logger.LogInformation(
            "Completed deletion operation. operation={Operation} ownerId={OwnerId} targetType={TargetType} targetId={TargetId} petCount={PetCount} healthLogCount={HealthLogCount} visitCount={VisitCount} scheduleItemCount={ScheduleItemCount} imageAssetCount={ImageAssetCount} storageTargetCount={StorageTargetCount} deletedReadyBytes={DeletedReadyBytes}",
            operation,
            ownerId,
            targetType,
            targetId,
            petCount,
            healthLogCount,
            visitCount,
            scheduleItemCount,
            imageAssetCount,
            storageTargetCount,
            deletedReadyBytes);
    }

    public static void LogDeletionFailed(
        ILogger logger,
        Exception exception,
        string operation,
        string ownerId,
        string targetType,
        object targetId,
        int petCount = 0,
        int healthLogCount = 0,
        int visitCount = 0,
        int scheduleItemCount = 0,
        int imageAssetCount = 0,
        int storageTargetCount = 0,
        long deletedReadyBytes = 0)
    {
        logger.LogError(
            exception,
            "Deletion operation failed. operation={Operation} ownerId={OwnerId} targetType={TargetType} targetId={TargetId} petCount={PetCount} healthLogCount={HealthLogCount} visitCount={VisitCount} scheduleItemCount={ScheduleItemCount} imageAssetCount={ImageAssetCount} storageTargetCount={StorageTargetCount} deletedReadyBytes={DeletedReadyBytes}",
            operation,
            ownerId,
            targetType,
            targetId,
            petCount,
            healthLogCount,
            visitCount,
            scheduleItemCount,
            imageAssetCount,
            storageTargetCount,
            deletedReadyBytes);
    }

    public static void LogUnhandledRequestException(
        ILogger logger,
        Exception exception,
        HttpContext httpContext)
    {
        logger.LogError(
            exception,
            "Unhandled request exception. method={Method} path={Path} traceId={TraceId} userId={UserId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier,
            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }
}
