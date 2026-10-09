using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Domain.Entities;

namespace Ayurveda.Infrastructure.Data;

public class AyurvedaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AyurvedaDbContext(DbContextOptions<AyurvedaDbContext> options) : base(options)
    {
    }

    // Legacy tables for compatibility & migration
    public DbSet<User> LegacyUsers => Set<User>();
    public DbSet<Admin> LegacyAdmins => Set<Admin>();

    // Core Domain tables
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorDetail> DoctorDetails => Set<DoctorDetail>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Yoga> Yogas => Set<Yoga>();
    public DbSet<YogaReview> YogaReviews => Set<YogaReview>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
            entity.Property(e => e.Gender).HasMaxLength(20);
        });

        // Legacy Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Contact).HasMaxLength(20);
        });

        // Legacy Admins
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("Admins");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.AdminName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
        });

        // Doctor
        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.ToTable("Doctors");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.ContactNumber).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.DoctorName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);

            entity.HasOne(d => d.DoctorDetail)
                  .WithOne(dd => dd.Doctor)
                  .HasForeignKey<DoctorDetail>(dd => dd.DoctorId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ApplicationUser)
                  .WithOne(u => u.DoctorProfile)
                  .HasForeignKey<Doctor>(d => d.ApplicationUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // DoctorDetail
        modelBuilder.Entity<DoctorDetail>(entity =>
        {
            entity.ToTable("DoctorDetails");
            entity.Property(e => e.ConsultationFee).HasPrecision(18, 2);

            entity.HasOne(dd => dd.Specialization)
                  .WithMany(c => c.DoctorSpecializations)
                  .HasForeignKey(dd => dd.SpecializationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        // Subcategory
        modelBuilder.Entity<Subcategory>(entity =>
        {
            entity.ToTable("Subcategories");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);

            entity.HasOne(s => s.Category)
                  .WithMany(c => c.Subcategories)
                  .HasForeignKey(s => s.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Mrp).HasPrecision(18, 2);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Subcategory)
                  .WithMany(s => s.Products)
                  .HasForeignKey(p => p.SubcategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages");
            entity.HasOne(pi => pi.Product)
                  .WithMany(p => p.Images)
                  .HasForeignKey(pi => pi.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductVariant
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("ProductVariants");
            entity.Property(v => v.Price).HasPrecision(18, 2);
            entity.Property(v => v.Mrp).HasPrecision(18, 2);

            entity.HasOne(pv => pv.Product)
                  .WithMany(p => p.Variants)
                  .HasForeignKey(pv => pv.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductQuestion
        modelBuilder.Entity<ProductQuestion>(entity =>
        {
            entity.ToTable("ProductQuestions");
            entity.Property(q => q.QuestionText).IsRequired().HasMaxLength(500);
            entity.Property(q => q.AnswerText).IsRequired();

            entity.HasOne(q => q.Product)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(q => q.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cart
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Carts");
            entity.HasOne(c => c.User)
                  .WithOne(u => u.Cart)
                  .HasForeignKey<Cart>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CartItem
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems");
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
            entity.ToTable("Orders");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.DeliveryFee).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);

            entity.HasOne(o => o.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
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

        // Address
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Addresses");
            entity.HasOne(a => a.User)
                  .WithMany(u => u.Addresses)
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Yoga
        modelBuilder.Entity<Yoga>(entity =>
        {
            entity.ToTable("Yogas");
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
            entity.ToTable("ProductReviews");
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
            entity.ToTable("YogaReviews");
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
            entity.ToTable("Appointments");
            entity.Property(e => e.ConsultationFee).HasPrecision(18, 2);

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

        // WishlistItem
        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.ToTable("WishlistItems");
            entity.HasOne(w => w.User)
                  .WithMany(u => u.Wishlist)
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.Product)
                  .WithMany(p => p.WishlistItems)
                  .HasForeignKey(w => w.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Coupon
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.ToTable("Coupons");
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.DiscountAmount).HasPrecision(18, 2);
            entity.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
            entity.Property(c => c.MaxDiscountAmount).HasPrecision(18, 2);
        });

        // Banner
        modelBuilder.Entity<Banner>(entity =>
        {
            entity.ToTable("Banners");
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityName).HasMaxLength(100);
        });

        // Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasOne(n => n.User)
                  .WithMany(u => u.Notifications)
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.Property(p => p.Amount).HasPrecision(18, 2);
        });

        // CallSession
        modelBuilder.Entity<CallSession>(entity =>
        {
            entity.ToTable("CallSessions");
        });

        // PasswordResetOtp
        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.ToTable("PasswordResetOtps");
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.OtpHashed).IsRequired().HasMaxLength(128);
        });
    }
}
