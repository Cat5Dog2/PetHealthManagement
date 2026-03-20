using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.Models;

public class Pet
{
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SpeciesCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    public bool IsPublic { get; set; } = true;

    public Guid? PhotoImageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<HealthLog> HealthLogs { get; set; } = [];

    public ICollection<ScheduleItem> ScheduleItems { get; set; } = [];
}
