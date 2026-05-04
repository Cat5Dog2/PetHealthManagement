using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.Identity.Manage;

public abstract class IdentityManagePageViewModel
{
    public string? StatusMessage { get; set; }
}

public sealed class IdentityManageProfileViewModel : IdentityManagePageViewModel
{
    public string Username { get; set; } = string.Empty;

    [Phone(ErrorMessage = "電話番号の形式が正しくありません。")]
    [Display(Name = "電話番号")]
    public string? PhoneNumber { get; set; }
}

public sealed class IdentityManageEmailViewModel : IdentityManagePageViewModel
{
    public string Email { get; set; } = string.Empty;

    public bool IsEmailConfirmed { get; set; }

    [Required(ErrorMessage = "メールアドレスを入力してください。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    [Display(Name = "新しいメールアドレス")]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class IdentityManageChangePasswordViewModel : IdentityManagePageViewModel
{
    [Required(ErrorMessage = "現在のパスワードを入力してください。")]
    [DataType(DataType.Password)]
    [Display(Name = "現在のパスワード")]
    public string OldPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新しいパスワードを入力してください。")]
    [StringLength(100, ErrorMessage = "{0} は {2} 文字以上 {1} 文字以内で入力してください。", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "新しいパスワード")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "新しいパスワード（確認）")]
    [Compare(nameof(NewPassword), ErrorMessage = "新しいパスワードと確認用パスワードが一致しません。")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class IdentityManageTwoFactorViewModel : IdentityManagePageViewModel
{
    public bool IsTwoFactorEnabled { get; set; }

    public bool IsMachineRemembered { get; set; }

    public int RecoveryCodesLeft { get; set; }
}
