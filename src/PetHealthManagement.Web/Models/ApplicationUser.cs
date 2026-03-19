using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PetHealthManagement.Web.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    public Guid? AvatarImageId { get; set; }

    public long UsedImageBytes { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
