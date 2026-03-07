namespace PetHealthManagement.Web.Models;

public class ImageAsset
{
    public Guid ImageId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public ImageAssetStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum ImageAssetStatus
{
    Pending = 0,
    Ready = 1
}
