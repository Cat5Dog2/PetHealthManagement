using System.ComponentModel.DataAnnotations;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.ViewModels.ScheduleItems;

public class ScheduleItemEditViewModel
{
    public int? ScheduleItemId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:" + InputValidationLimits.DateInputFormat + "}", ApplyFormatInEditMode = true)]
    public DateTime? DueDate { get; set; }

    [Required]
    [StringLength(InputValidationLimits.ScheduleItems.ItemTypeMaxLength)]
    public string ItemType { get; set; } = string.Empty;

    [Required]
    [StringLength(InputValidationLimits.ScheduleItems.TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    [StringLength(InputValidationLimits.ScheduleItems.NoteMaxLength)]
    public string? Note { get; set; }

    public bool IsDone { get; set; }

    public string? RowVersion { get; set; }

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";

    public List<ScheduleItemTypeOptionViewModel> TypeOptions { get; set; } = [];
}

public class ScheduleItemTypeOptionViewModel
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
