using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.Identity.Account;

public sealed class IdentityLoginViewModel
{
    [Required(ErrorMessage = "メールアドレスを入力してください。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    [Display(Name = "メールアドレス")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードを入力してください。")]
    [DataType(DataType.Password)]
    [Display(Name = "パスワード")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "ログイン状態を保持する")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class IdentityRegisterViewModel
{
    [Required(ErrorMessage = "メールアドレスを入力してください。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    [Display(Name = "メールアドレス")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "パスワードを入力してください。")]
    [StringLength(100, ErrorMessage = "{0} は {2} 文字以上 {1} 文字以内で入力してください。", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "パスワード")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "パスワード（確認）")]
    [Compare(nameof(Password), ErrorMessage = "パスワードと確認用パスワードが一致しません。")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
