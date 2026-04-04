namespace PetHealthManagement.Web.Services;

public class DevelopmentSetupOptions
{
    public const string SectionName = "DevelopmentSetup";

    public string AdminEmail { get; set; } = "admin@example.com";

    public string AdminPassword { get; set; } = string.Empty;

    public string AdminDisplayName { get; set; } = "Development Admin";
}
