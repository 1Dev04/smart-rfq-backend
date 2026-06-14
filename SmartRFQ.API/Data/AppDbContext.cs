using System.Security.AccessControl;
using Microsoft.EntityFrameworkCore;
using SmartRFQ.API.Models;

namespace SmartRFQ.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AudioLogs> AudioLogs => Set<AudioLogs>();

    protected override void OnModelCreating(ModelBuilder b)
    {
       

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasMaxLength(20);
            e.Property(u => u.Email).HasMaxLength(150);
            e.Property(u => u.FullName).HasMaxLength(200);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshToken");
            e.HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
        });

         b.Entity<AudioLogs>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasIndex(x => x.DateTime);
            e.HasIndex(x => x.E_User);
            e.HasIndex(x => x.RfqNo);
            e.HasIndex(x => x.Status);
        });
    }
}