
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using SmartRFQ.API.Models;

namespace SmartRFQ.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLogs> AudioLogs => Set<AuditLogs>();

    public DbSet<DocRequest> DocRequests => Set<DocRequest>();

    public DbSet<GLCodes> GLCodes => Set<GLCodes>();
    public DbSet<SapCodes> SapCodes => Set<SapCodes>();

    public DbSet<DocRequestItem> DocRequestItems => Set<DocRequestItem>();

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

        b.Entity<AuditLogs>(e =>
       {
           e.ToTable("AuditLogs");
           e.HasIndex(x => x.DateTime);
           e.HasIndex(x => x.E_User);
           e.HasIndex(x => x.RfqNo);
           e.HasIndex(x => x.Status);
       });

        b.Entity<GLCodes>(e =>
   {
       e.ToTable("GL_CodeList");
   });

        b.Entity<DocRequest>(e =>
        {
            e.ToTable("DocRequests");
            e.HasIndex(x => x.RfqNo).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(d => d.Requester)
         .WithMany()
         .HasForeignKey(d => d.RequesterId)
         .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Purchaser)
            .WithMany()
            .HasForeignKey(d => d.PurchaserId)
            .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DocRequestItem>(e =>
        {
            e.ToTable("DocRequestItems");
            e.HasIndex(x => x.DocRequestId);
            e.HasOne(i => i.DocRequest)
         .WithMany(d => d.Items)
         .HasForeignKey(i => i.DocRequestId)
         .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.ProjectName).HasMaxLength(150);
            e.Property(x => x.ItemDescription).HasMaxLength(500);
            e.Property(x => x.SpecPartNo).HasMaxLength(200);
            e.Property(x => x.Brand).HasMaxLength(100);
            e.Property(x => x.ForGas).HasMaxLength(100);
            e.Property(x => x.SpecPurity).HasMaxLength(100);
            e.Property(x => x.CylinderType).HasMaxLength(100);
            e.Property(x => x.Uom).HasMaxLength(20);
            e.Property(x => x.CylinderSize).HasMaxLength(20);
            e.Property(x => x.MakerSource).HasMaxLength(150);
            e.Property(x => x.RequiredValve).HasMaxLength(100);
            e.Property(x => x.PurposeApplication).HasMaxLength(300);
            e.Property(x => x.Customer).HasMaxLength(150);
            e.Property(x => x.AddressLocation).HasMaxLength(300);
            e.Property(x => x.RecommendVendor).HasMaxLength(200);
            e.Property(x => x.Remark).HasMaxLength(500);


        });
          b.Entity<SapCodes>(e =>
   {
       e.ToTable("SAP_CodeList");
   });

    }
}

