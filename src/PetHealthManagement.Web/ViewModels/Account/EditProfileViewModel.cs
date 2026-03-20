using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.Account;

public class EditProfileViewModel
{
    [StringLength(50)]
    public string? DisplayName { get; set; }

    public IFormFile? AvatarFile { get; set; }

    public string CurrentAvatarUrl { get; set; } = "/images/default/avatar-placeholder.svg";

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}
