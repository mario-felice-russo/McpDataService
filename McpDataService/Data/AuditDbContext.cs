using Microsoft.EntityFrameworkCore;
using McpDataService.Models.Audit;

namespace McpDataService.Data;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }
    public DbSet<AuditRequest> Requests { get; set; } = null!;
    public DbSet<AuditRequestParameter> RequestParameters { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditRequest>()
            .HasMany(r => r.Parameters)
            .WithOne(p => p.Request)
            .HasForeignKey(p => p.RequestId);

        modelBuilder.Entity<AuditRequest>()
            .HasIndex(r => r.Timestamp);
        modelBuilder.Entity<AuditRequest>()
            .HasIndex(r => r.Endpoint);
        modelBuilder.Entity<AuditRequest>()
            .HasIndex(r => r.UserType);
        modelBuilder.Entity<AuditRequest>()
            .HasIndex(r => r.DbType);
        modelBuilder.Entity<AuditRequest>()
            .HasIndex(r => r.IsMcpRequest);
    }
}