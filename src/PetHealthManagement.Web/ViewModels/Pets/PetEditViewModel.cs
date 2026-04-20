using System.ComponentModel.DataAnnotations;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.ViewModels.Pets;

public class PetEditViewModel
{
    public int? PetId { get; set; }

    [Required]
    [StringLength(InputValidationLimits.Pets.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(InputValidationLimits.Pets.SpeciesCodeMaxLength)]
    public string SpeciesCode { get; set; } = string.Empty;

    [StringLength(InputValidationLimits.Pets.BreedMaxLength)]
    public string? Breed { get; set; }

    [StringLength(InputValidationLimits.Pets.SexMaxLength)]
    public string? Sex { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:" + InputValidationLimits.DateInputFormat + "}", ApplyFormatInEditMode = true)]
    public DateTime? BirthDate { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:" + InputValidationLimits.DateInputFormat + "}", ApplyFormatInEditMode = true)]
    public DateTime? AdoptedDate { get; set; }

    public bool IsPublic { get; set; } = true;

    public IFormFile? PhotoFile { get; set; }

    public bool RemovePhoto { get; set; }

    public string? CurrentPhotoUrl { get; set; }

    public string? RowVersion { get; set; }

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/Pets";

    public List<SpeciesOptionViewModel> SpeciesOptions { get; set; } = [];
}
