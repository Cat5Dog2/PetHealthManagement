namespace PetHealthManagement.Web.ViewModels.Account;

public class DeleteAccountViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}
