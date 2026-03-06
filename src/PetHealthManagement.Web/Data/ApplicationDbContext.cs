using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Pet> Pets => Set<Pet>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Pet>(entity =>
        {
            entity.ToTable("Pets");

            entity.Property(p => p.OwnerId)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(p => p.Name)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.SpeciesCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.Breed)
                .HasMaxLength(100);

            entity.Property(p => p.IsPublic)
                .HasDefaultValue(true);

            entity.HasIndex(p => p.OwnerId);
            entity.HasIndex(p => new { p.IsPublic, p.SpeciesCode });
            entity.HasIndex(p => p.UpdatedAt);
        });
    }
}
