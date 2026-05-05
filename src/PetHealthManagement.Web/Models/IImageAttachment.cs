namespace PetHealthManagement.Web.Models;

public interface IImageAttachment
{
    Guid ImageId { get; set; }

    ImageAsset Image { get; set; }

    int SortOrder { get; set; }
}
