namespace PetHealthManagement.Web.ViewModels.Pets;

public class PetDetailsViewModel
{
    public int PetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SpeciesLabel { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public string OwnerDisplayName { get; set; } = string.Empty;

    public bool IsPublic { get; set; }

    public bool IsOwner { get; set; }

    public string ReturnUrl { get; set; } = "/Pets";
}
