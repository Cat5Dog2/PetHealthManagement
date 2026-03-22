namespace PetHealthManagement.Web.Models;

public class VisitImage
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public Guid ImageId { get; set; }

    public ImageAsset Image { get; set; } = null!;

    public int SortOrder { get; set; }
}
