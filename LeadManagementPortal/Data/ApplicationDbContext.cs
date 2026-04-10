using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Lead> Leads { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<SalesGroup> SalesGroups { get; set; }
        public DbSet<SalesOrg> SalesOrgs { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<SystemSettings> Settings { get; set; }
        public DbSet<LeadExtensionAudit> LeadExtensionAudits { get; set; }
        public DbSet<CustomerAudit> CustomerAudits { get; set; }
        public DbSet<LeadAudit> LeadAudits { get; set; }
        public DbSet<LeadDocument> LeadDocuments { get; set; }
        public DbSet<LeadFollowUpTask> LeadFollowUpTasks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CommissionDeal> CommissionDeals { get; set; }
        public DbSet<CommissionLink> CommissionLinks { get; set; }
        public DbSet<SaleRecord> SaleRecords { get; set; }
        public DbSet<CommissionLedger> CommissionLedgers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Lead Configuration
            builder.Entity<Lead>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.ExpiryDate);

                entity.HasOne(e => e.AssignedTo)
                    .WithMany(u => u.Leads)
                    .HasForeignKey(e => e.AssignedToId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SalesGroup)
                    .WithMany(g => g.Leads)
                    .HasForeignKey(e => e.SalesGroupId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Customer Configuration
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.ConversionDate);

                entity.HasOne(e => e.ConvertedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ConvertedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SalesRep)
                    .WithMany()
                    .HasForeignKey(e => e.SalesRepId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.SalesGroup)
                    .WithMany()
                    .HasForeignKey(e => e.SalesGroupId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SalesGroup Configuration
            builder.Entity<SalesGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();

                entity.HasOne(e => e.GroupAdmin)
                    .WithMany()
                    .HasForeignKey(e => e.GroupAdminId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.SalesOrgs)
                    .WithOne(o => o.SalesGroup)
                    .HasForeignKey(o => o.SalesGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ApplicationUser Configuration
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(e => e.SalesGroup)
                    .WithMany(g => g.SalesReps)
                    .HasForeignKey(e => e.SalesGroupId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.SalesOrg)
                    .WithMany(o => o.SalesReps)
                    .HasForeignKey(e => e.SalesOrgId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CommissionDeal)
                    .WithOne(d => d.ApplicationUser)
                    .HasForeignKey<CommissionDeal>(d => d.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SponsorLink)
                    .WithOne(l => l.Downline)
                    .HasForeignKey<CommissionLink>(l => l.DownlineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SalesOrg Configuration
            builder.Entity<SalesOrg>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
            });

            // Product Configuration
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // System Settings Configuration
            builder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // Seed default settings
            builder.Entity<SystemSettings>().HasData(new SystemSettings
            {
                Id = 1,
                CoolingPeriodDays = 15,
                LeadInitialExpiryDays = 15,
                LeadExtensionDays = 5
            });

            // Lead-Product Many-to-Many
            builder.Entity<Lead>()
                .HasMany(l => l.Products)
                .WithMany(p => p.Leads)
                .UsingEntity<Dictionary<string, object>>(
                    "LeadProducts",
                    j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Lead>().WithMany().HasForeignKey("LeadId").OnDelete(DeleteBehavior.Cascade)
                );

            // LeadExtensionAudit configuration
            builder.Entity<LeadExtensionAudit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GrantedAtUtc);
                entity.HasOne(e => e.Lead)
                    .WithMany()
                    .HasForeignKey(e => e.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CustomerAudit configuration
            builder.Entity<CustomerAudit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OccurredAtUtc);
                entity.Property(e => e.Action).HasMaxLength(64);
                entity.Property(e => e.Term).HasMaxLength(512);
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Customer>()
                    .WithMany()
                    .HasForeignKey(e => e.TargetCustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // LeadAudit configuration
            builder.Entity<LeadAudit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OccurredAtUtc);
                entity.Property(e => e.Action).HasMaxLength(64);
                entity.Property(e => e.Details).HasMaxLength(2048);
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Lead>()
                    .WithMany()
                    .HasForeignKey(e => e.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // LeadDocument configuration
            builder.Entity<LeadDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LeadId);
                entity.Property(e => e.FileName).HasMaxLength(260).IsRequired();
                entity.Property(e => e.ContentType).HasMaxLength(200);
                entity.Property(e => e.StorageKey).HasMaxLength(500).IsRequired();
                entity.HasOne<Lead>()
                    .WithMany(l => l.Documents)
                    .HasForeignKey(e => e.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // LeadFollowUpTask configuration
            builder.Entity<LeadFollowUpTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LeadId);
                entity.HasIndex(e => e.DueDate);
                entity.HasIndex(e => e.IsCompleted);
                entity.Property(e => e.Type).HasMaxLength(32).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
                entity.HasOne(e => e.Lead)
                    .WithMany(l => l.FollowUpTasks)
                    .HasForeignKey(e => e.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Fix FK delete behaviors to avoid multiple cascade paths
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.SalesGroup)
                    .WithMany(g => g.SalesReps)
                    .HasForeignKey(u => u.SalesGroupId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(u => u.SalesOrg)
                    .WithMany(o => o.SalesReps)
                    .HasForeignKey(u => u.SalesOrgId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SalesOrg>(entity =>
            {
                entity.HasOne(s => s.SalesGroup)
                    .WithMany(g => g.SalesOrgs)
                    .HasForeignKey(s => s.SalesGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Notification configuration
            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Role);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsRead);
                entity.Property(e => e.Type).HasMaxLength(64);
                entity.Property(e => e.Title).HasMaxLength(256);
                entity.Property(e => e.Message).HasMaxLength(1024);
                entity.Property(e => e.Link).HasMaxLength(512);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false);
            });

            builder.Entity<CommissionDeal>(entity =>
            {
                entity.HasKey(e => e.ApplicationUserId);
                entity.Property(e => e.Rate).HasPrecision(18, 2);
                entity.Property(e => e.BaseCost).HasPrecision(18, 2);
            });

            builder.Entity<CommissionLink>(entity =>
            {
                entity.HasKey(e => e.DownlineId);
                entity.HasIndex(e => e.SponsorId);
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_CommissionLinks_NoSelfSponsor", "[DownlineId] <> [SponsorId]");
                });

                entity.HasOne(e => e.Sponsor)
                    .WithMany(u => u.SponsoredDownlines)
                    .HasForeignKey(e => e.SponsorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SaleRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AccountId);
                entity.HasIndex(e => e.SaleDate);
                entity.HasIndex(e => e.ImportBatchId);
                entity.Property(e => e.ProductName).HasMaxLength(256).IsRequired();
                entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
                entity.Property(e => e.CostAmount).HasPrecision(18, 2);
                entity.Property(e => e.ImportBatchId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.RawPayload).IsRequired();

                entity.HasOne(e => e.Account)
                    .WithMany(u => u.SaleRecords)
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CommissionLedger>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SaleRecordId);
                entity.HasIndex(e => e.BeneficiaryId);
                entity.HasIndex(e => new { e.SaleRecordId, e.BeneficiaryId }).IsUnique();
                entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
                entity.Property(e => e.NetAmount).HasPrecision(18, 2);
                entity.Property(e => e.CommissionAmount).HasPrecision(18, 2);
                entity.Property(e => e.DealSnapshot).IsRequired();
                entity.Property(e => e.CalculationNotes).IsRequired();

                entity.HasOne(e => e.SaleRecord)
                    .WithMany(s => s.CommissionLedgers)
                    .HasForeignKey(e => e.SaleRecordId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Beneficiary)
                    .WithMany(u => u.CommissionLedgers)
                    .HasForeignKey(e => e.BeneficiaryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
