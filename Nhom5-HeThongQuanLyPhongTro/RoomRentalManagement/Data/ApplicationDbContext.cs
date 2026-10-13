using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Room> Rooms { get; set; }

        public DbSet<Tenant> Tenants { get; set; }

        public DbSet<Contract> Contracts { get; set; }

        public DbSet<Invoice> Invoices { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<Request> Requests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User - Tenant
            modelBuilder.Entity<Tenant>()
                .HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<Tenant>(t => t.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            // Room - Contract
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Room)
                .WithMany()
                .HasForeignKey(c => c.RoomID)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant - Contract
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantID)
                .OnDelete(DeleteBehavior.Restrict);

            // Contract - Invoice
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Contract)
                .WithMany()
                .HasForeignKey(i => i.ContractID)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice - Payment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany()
                .HasForeignKey(p => p.InvoiceID)
                .OnDelete(DeleteBehavior.Cascade);

            // Tenant - Request
            modelBuilder.Entity<Request>()
                .HasOne(r => r.Tenant)
                .WithMany()
                .HasForeignKey(r => r.TenantID)
                .OnDelete(DeleteBehavior.Cascade);

            // Decimal precision
            modelBuilder.Entity<Room>()
                .Property(r => r.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Room>()
                .Property(r => r.Area)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Contract>()
                .Property(c => c.Deposit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Contract>()
                .Property(c => c.RentPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.ElectricOld)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.ElectricNew)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.ElectricPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.WaterOld)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.WaterNew)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.WaterPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.RentAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.ServiceFee)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);
        }
    }
}