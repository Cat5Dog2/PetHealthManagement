using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.Visits;

public class VisitEditViewModel
{
    public int? VisitId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime? VisitDate { get; set; }

    [StringLength(100)]
    public string? ClinicName { get; set; }

    [StringLength(500)]
    public string? Diagnosis { get; set; }

    [StringLength(500)]
    public string? Prescription { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public List<VisitExistingImageViewModel> ExistingImages { get; set; } = [];

    public List<IFormFile> NewFiles { get; set; } = [];

    public Guid[] DeleteImageIds { get; set; } = [];

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}

public class VisitExistingImageViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}
