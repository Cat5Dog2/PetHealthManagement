namespace PetHealthManagement.Web.ViewModels.Admin.Users;

public class AdminUserIndexViewModel
{
    public const int DefaultPageSize = 10;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; }

    public List<AdminUserListItemViewModel> Users { get; set; } = [];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public class AdminUserListItemViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public int PetCount { get; set; }
}
