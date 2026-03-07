using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.Pets;

public class PetEditViewModel
{
    public int? PetId { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SpeciesCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Breed { get; set; }

    public bool IsPublic { get; set; } = true;

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/Pets";

    public List<SpeciesOptionViewModel> SpeciesOptions { get; set; } = [];
}
