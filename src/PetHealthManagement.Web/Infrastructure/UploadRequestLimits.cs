namespace PetHealthManagement.Web.Infrastructure;

public static class UploadRequestLimits
{
    // 10 x 2MB images plus multipart overhead for the rest of the form.
    public const long MaxMultipartRequestBodySizeBytes = 25 * 1024 * 1024;
}
