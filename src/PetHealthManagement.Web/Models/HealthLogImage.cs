namespace PetHealthManagement.Web.Models;

public class HealthLogImage
{
    public int Id { get; set; }

    public int HealthLogId { get; set; }

    public HealthLog HealthLog { get; set; } = null!;

    public Guid ImageId { get; set; }

    public ImageAsset Image { get; set; } = null!;

    public int SortOrder { get; set; }
}
