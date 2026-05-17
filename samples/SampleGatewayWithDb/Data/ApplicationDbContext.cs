using Microsoft.EntityFrameworkCore;

namespace SampleGatewayWithDb.Data;

/// <summary>
/// EF Core DbContext for tenant configuration.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<TenantEntity> Tenants { get; set; } = null!;

    public DbSet<EndpointEntity> Endpoints { get; set; } = null!;

    public DbSet<TenantHeaderEntity> TenantHeaders { get; set; } = null!;

    public DbSet<CertificateEntity> Certificates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Tenant entity
        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.TenantId).IsUnique();
            entity.Property(t => t.TenantId).HasMaxLength(256).IsRequired();
            entity.Property(t => t.DefaultEndpointName).HasMaxLength(256);

            entity.HasMany(t => t.Endpoints)
                .WithOne(e => e.Tenant)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.DefaultHeaders)
                .WithOne(h => h.Tenant)
                .HasForeignKey(h => h.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Certificate)
                .WithOne(c => c.Tenant)
                .HasForeignKey<CertificateEntity>(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Endpoint entity
        modelBuilder.Entity<EndpointEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.BaseAddress).HasMaxLength(1000).IsRequired();

            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Endpoints)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Certificate)
                .WithOne(c => c.Endpoint)
                .HasForeignKey<CertificateEntity>(c => c.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TenantHeader entity
        modelBuilder.Entity<TenantHeaderEntity>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Key).HasMaxLength(256).IsRequired();
            entity.Property(h => h.Value).HasMaxLength(1000).IsRequired();

            entity.HasOne(h => h.Tenant)
                .WithMany(t => t.DefaultHeaders)
                .HasForeignKey(h => h.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Certificate entity
        modelBuilder.Entity<CertificateEntity>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Type).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Path).HasMaxLength(500);
            entity.Property(c => c.Password).HasMaxLength(256);
            entity.Property(c => c.Data).HasColumnType("ntext");
        });
    }
}
