using KerzelPay.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KerzelPay.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<Transfer> Transfers { get; set; }
        public DbSet<Agent> Agents { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Account: unique SerialNumber
            builder.Entity<Account>()
                .HasIndex(a => a.SerialNumber)
                .IsUnique();

            // Currency: unique Code
            builder.Entity<Currency>()
                .HasIndex(c => c.Code)
                .IsUnique();

            // Transfer: unique TrackingNumber
            builder.Entity<Transfer>()
                .HasIndex(t => t.TrackingNumber)
                .IsUnique();

            // Transfer relationships — prevent multiple cascade paths
            builder.Entity<Transfer>()
                .HasOne(t => t.SourceAccount)
                .WithMany()
                .HasForeignKey(t => t.SourceAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Transfer>()
                .HasOne(t => t.DestinationAccount)
                .WithMany()
                .HasForeignKey(t => t.DestinationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Agent: one user can only be one agent
            builder.Entity<Agent>()
                .HasIndex(a => a.UserId)
                .IsUnique();
        }
    }
}