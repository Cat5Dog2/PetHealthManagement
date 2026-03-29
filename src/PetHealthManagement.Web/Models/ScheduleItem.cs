using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.Models;

public class ScheduleItem
{
    public int Id { get; set; }

    public int PetId { get; set; }

    public Pet Pet { get; set; } = null!;

    public DateTime DueDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Note { get; set; }

    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
