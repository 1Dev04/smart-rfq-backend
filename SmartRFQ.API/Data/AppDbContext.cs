using Microsoft.EntityFrameworkCore;
using SmartRFQ.API.Models;

namespace SmartRFQ.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AudioLogs> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioLogs>(entity =>
 {
     entity.ToTable("AuditLogs");
     entity.HasIndex(x => x.DateTime);
     entity.HasIndex(x => x.E_User);
     entity.HasIndex(x => x.RfqNo);
     entity.HasIndex(x => x.Status);
 });
    }
}