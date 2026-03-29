using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.Models;

public class Visit
{
    public int Id { get; set; }

    public int PetId { get; set; }

    public Pet Pet { get; set; } = null!;

    public DateTime VisitDate { get; set; }

    [MaxLength(100)]
    public string? ClinicName { get; set; }

    [MaxLength(500)]
    public string? Diagnosis { get; set; }

    [MaxLength(500)]
    public string? Prescription { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<VisitImage> Images { get; set; } = [];
}
