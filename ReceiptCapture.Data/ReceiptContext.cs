// ReceiptCapture.Data/ReceiptContext.cs
using Microsoft.EntityFrameworkCore;
using ReceiptCapture.Data.Models;

namespace ReceiptCapture.Data;

public class ReceiptContext : DbContext
{
    public ReceiptContext(DbContextOptions<ReceiptContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);  // Explicit key
            entity.HasIndex(e => e.TelegramUserId).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId);  // Explicit key
        });

        // Receipt configuration
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId);  // Explicit key
            entity.HasIndex(e => new { e.UserId, e.UploadedAt });
            entity.HasIndex(e => e.ReceiptDate);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Currency).HasDefaultValue("MYR");
        });

        // ReceiptItem configuration - THIS WAS MISSING
        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);  // Explicit key
            entity.HasOne(e => e.Receipt)
                  .WithMany(r => r.Items)
                  .HasForeignKey(e => e.ReceiptId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed categories
        modelBuilder.Entity<Category>().HasData(
            new Category { CategoryId = 1, Name = "Food & Dining", Description = "Restaurants, cafes, groceries", ColorCode = "#FF6B6B", Icon = "fa-utensils", IsDefault = true },
            new Category { CategoryId = 2, Name = "Transportation", Description = "Fuel, parking, public transport", ColorCode = "#4ECDC4", Icon = "fa-car", IsDefault = true },
            new Category { CategoryId = 3, Name = "Shopping", Description = "Clothing, electronics, general shopping", ColorCode = "#45B7D1", Icon = "fa-shopping-bag", IsDefault = true },
            new Category { CategoryId = 4, Name = "Entertainment", Description = "Movies, games, subscriptions", ColorCode = "#96CEB4", Icon = "fa-film", IsDefault = true },
            new Category { CategoryId = 5, Name = "Utilities", Description = "Electric, water, internet, phone", ColorCode = "#FFEAA7", Icon = "fa-bolt", IsDefault = true },
            new Category { CategoryId = 6, Name = "Healthcare", Description = "Medical, pharmacy, insurance", ColorCode = "#DDA0DD", Icon = "fa-medkit", IsDefault = true },
            new Category { CategoryId = 7, Name = "Education", Description = "Courses, books, training", ColorCode = "#98D8C8", Icon = "fa-graduation-cap", IsDefault = true },
            new Category { CategoryId = 8, Name = "Others", Description = "Miscellaneous expenses", ColorCode = "#B0B0B0", Icon = "fa-tag", IsDefault = true }
        );
    }
}