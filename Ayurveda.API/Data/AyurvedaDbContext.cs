using Microsoft.EntityFrameworkCore;
using Ayurveda.API.Models;

namespace Ayurveda.API.Data;

public class AyurvedaDbContext : DbContext
{
    public AyurvedaDbContext(DbContextOptions<AyurvedaDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorDetail> DoctorDetails => Set<DoctorDetail>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Yoga> Yogas => Set<Yoga>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<YogaReview> YogaReviews => Set<YogaReview>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Contact).HasMaxLength(20);
        });

        // Admin
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.AdminName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
        });

        // Doctor
        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.ContactNumber).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.DoctorName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);

            entity.HasOne(d => d.DoctorDetail)
                  .WithOne(dd => dd.Doctor)
                  .HasForeignKey<DoctorDetail>(dd => dd.DoctorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DoctorDetail
        modelBuilder.Entity<DoctorDetail>(entity =>
        {
            entity.HasOne(dd => dd.Specialization)
                  .WithMany(c => c.DoctorSpecializations)
                  .HasForeignKey(dd => dd.SpecializationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Cart
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithOne(u => u.Cart)
                  .HasForeignKey<Cart>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CartItem
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasOne(ci => ci.Cart)
                  .WithMany(c => c.CartItems)
                  .HasForeignKey(ci => ci.CartId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ci => ci.Product)
                  .WithMany(p => p.CartItems)
                  .HasForeignKey(ci => ci.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);

            entity.HasOne(o => o.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Yoga
        modelBuilder.Entity<Yoga>(entity =>
        {
            entity.HasIndex(e => e.YogaName).IsUnique();
            entity.Property(e => e.YogaName).IsRequired().HasMaxLength(150);

            entity.HasOne(y => y.Category)
                  .WithMany(c => c.Yogas)
                  .HasForeignKey(y => y.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductReview
        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasOne(pr => pr.User)
                  .WithMany(u => u.ProductReviews)
                  .HasForeignKey(pr => pr.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pr => pr.Product)
                  .WithMany(p => p.Reviews)
                  .HasForeignKey(pr => pr.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // YogaReview
        modelBuilder.Entity<YogaReview>(entity =>
        {
            entity.HasOne(yr => yr.User)
                  .WithMany(u => u.YogaReviews)
                  .HasForeignKey(yr => yr.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(yr => yr.Yoga)
                  .WithMany(y => y.Reviews)
                  .HasForeignKey(yr => yr.YogaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Appointment
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(e => new { e.DoctorId, e.StartUtc })
                  .IsUnique()
                  .HasFilter("[Status] NOT IN ('Cancelled', 'Declined')");

            entity.HasOne(a => a.Doctor)
                  .WithMany(d => d.Appointments)
                  .HasForeignKey(a => a.DoctorId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.User)
                  .WithMany(u => u.Appointments)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PasswordResetOtp
        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.OtpHashed).IsRequired().HasMaxLength(128);
        });
    }
}
