using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using API.Model;
using API.Model.ExcelExport;
using Backend.Model;

namespace API.DBContext
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }


        public DbSet<User> Users { get; set; }
        public DbSet<TokenModel> TokenModels { get; set; }
        public DbSet<SystemSetting> SystemSetting { get; set; }
        public DbSet<ChatModel> ChatModels { get; set; }
        public DbSet<ExcelExportJob> ExcelExportJobs { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // modelBuilder.Entity<GateModel>().Property(e => e.CreatedDate)
            // .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            // // Adding the code below tells DB "NumericId is an AlternateKey and don't update".
            // // modelBuilder.Entity<CertificateModel>().Property(e => e.applicationNo)
            // // .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            modelBuilder.Entity<SystemSetting>()
                .Property(e => e.IMAmount)
                .HasPrecision(18, 4); // e.g., 18 total digits, 4 after decimal

            modelBuilder.Entity<SystemSetting>()
                .Property(e => e.MOCAmount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<SystemSetting>()
                .Property(e => e.OnlineFees)
                .HasPrecision(18, 4);

            modelBuilder.Entity<ExcelExportJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReportKey).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ReportTitle).HasMaxLength(256);
                entity.Property(e => e.FilterHash).HasMaxLength(64).IsRequired();
                entity.Property(e => e.FileName).HasMaxLength(260);
                entity.Property(e => e.RequestedByUserName).HasMaxLength(256);
                // Dedup lookup: find existing jobs by (hash, status).
                entity.HasIndex(e => new { e.FilterHash, e.Status });
                // Worker poll: oldest Queued first.
                entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
                // Cleanup sweep.
                entity.HasIndex(e => e.ExpiresAtUtc);
            });
        }
    }
}
