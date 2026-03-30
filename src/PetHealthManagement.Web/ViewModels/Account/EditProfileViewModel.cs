using System.ComponentModel.DataAnnotations;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.ViewModels.Account;

public class EditProfileViewModel
{
    [StringLength(InputValidationLimits.Profile.DisplayNameMaxLength)]
    public string? DisplayName { get; set; }

    public IFormFile? AvatarFile { get; set; }

    public string CurrentAvatarUrl { get; set; } = "/images/default/avatar-placeholder.svg";

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}
