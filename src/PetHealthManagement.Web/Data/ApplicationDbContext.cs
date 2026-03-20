using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<HealthLog> HealthLogs => Set<HealthLog>();
    public DbSet<HealthLogImage> HealthLogImages => Set<HealthLogImage>();
    public DbSet<ImageAsset> ImageAssets => Set<ImageAsset>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName)
                .HasMaxLength(50)
                .HasDefaultValue(string.Empty)
                .IsRequired();

            entity.Property(x => x.UsedImageBytes)
                .HasDefaultValue(0L)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne<ImageAsset>()
                .WithMany()
                .HasForeignKey(x => x.AvatarImageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

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

            entity.Property(p => p.PhotoImageId);

            entity.HasIndex(p => p.OwnerId);
            entity.HasIndex(p => new { p.IsPublic, p.SpeciesCode });
            entity.HasIndex(p => p.UpdatedAt);
        });

        builder.Entity<HealthLog>(entity =>
        {
            entity.ToTable("HealthLogs");

            entity.Property(x => x.RecordedAt)
                .IsRequired();

            entity.Property(x => x.StoolCondition)
                .HasMaxLength(50);

            entity.Property(x => x.Note)
                .HasMaxLength(1000);

            entity.HasIndex(x => new { x.PetId, x.RecordedAt, x.Id });

            entity.HasOne(x => x.Pet)
                .WithMany(x => x.HealthLogs)
                .HasForeignKey(x => x.PetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HealthLogImage>(entity =>
        {
            entity.ToTable("HealthLogImages");

            entity.HasIndex(x => new { x.HealthLogId, x.SortOrder });

            entity.HasIndex(x => x.ImageId)
                .IsUnique();

            entity.HasOne(x => x.HealthLog)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.HealthLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Image)
                .WithMany()
                .HasForeignKey(x => x.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ImageAsset>(entity =>
        {
            entity.ToTable("ImageAssets");
            entity.HasKey(x => x.ImageId);

            entity.Property(x => x.StorageKey)
                .HasMaxLength(260)
                .IsRequired();

            entity.Property(x => x.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.OwnerId)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.Category)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(x => new { x.OwnerId, x.Status });
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}
