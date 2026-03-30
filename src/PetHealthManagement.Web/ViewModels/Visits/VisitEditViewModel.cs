using System.ComponentModel.DataAnnotations;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.ViewModels.Visits;

public class VisitEditViewModel
{
    public int? VisitId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:" + InputValidationLimits.DateInputFormat + "}", ApplyFormatInEditMode = true)]
    public DateTime? VisitDate { get; set; }

    [StringLength(InputValidationLimits.Visits.ClinicNameMaxLength)]
    public string? ClinicName { get; set; }

    [StringLength(InputValidationLimits.Visits.DiagnosisMaxLength)]
    public string? Diagnosis { get; set; }

    [StringLength(InputValidationLimits.Visits.PrescriptionMaxLength)]
    public string? Prescription { get; set; }

    [StringLength(InputValidationLimits.Visits.NoteMaxLength)]
    public string? Note { get; set; }

    public List<VisitExistingImageViewModel> ExistingImages { get; set; } = [];

    public List<IFormFile> NewFiles { get; set; } = [];

    public Guid[] DeleteImageIds { get; set; } = [];

    public string? RowVersion { get; set; }

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}

public class VisitExistingImageViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}
