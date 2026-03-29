using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.Models;

public class HealthLog
{
    public int Id { get; set; }

    public int PetId { get; set; }

    public Pet Pet { get; set; } = null!;

    public DateTimeOffset RecordedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    [Range(0.0, 200.0)]
    public double? WeightKg { get; set; }

    [Range(0, 5000)]
    public int? FoodAmountGram { get; set; }

    [Range(0, 1440)]
    public int? WalkMinutes { get; set; }

    [MaxLength(50)]
    public string? StoolCondition { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public ICollection<HealthLogImage> Images { get; set; } = [];
}
