using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<PickupLocation> PickupLocations => Set<PickupLocation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<FoodDrop> FoodDrops => Set<FoodDrop>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SellerProfile>()
            .HasOne(sp => sp.User)
            .WithOne(u => u.SellerProfile)
            .HasForeignKey<SellerProfile>(sp => sp.UserId);

        modelBuilder.Entity<PickupLocation>()
            .HasOne(pl => pl.SellerProfile)
            .WithMany(sp => sp.PickupLocations)
            .HasForeignKey(pl => pl.SellerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FoodDrop>()
            .HasOne(f => f.Category)
            .WithMany(c => c.FoodDrops)
            .HasForeignKey(f => f.CategoryId);

        modelBuilder.Entity<FoodDrop>()
            .HasOne(f => f.Seller)
            .WithMany()
            .HasForeignKey(f => f.SellerId);

        modelBuilder.Entity<FoodDrop>()
            .HasOne(f => f.PickupLocation)
            .WithMany()
            .HasForeignKey(f => f.PickupLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.FoodDrop)
            .WithMany(f => f.Orders)
            .HasForeignKey(o => o.FoodDropId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Buyer)
            .WithMany()
            .HasForeignKey(o => o.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId);

        modelBuilder.Entity<Payout>()
            .HasOne(p => p.Seller)
            .WithMany()
            .HasForeignKey(p => p.SellerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // An order points at the payout that settled it. SetNull rather than
        // Cascade: deleting a payout record must never delete the orders it
        // paid for.
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payout)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.PayoutId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Order)
            .WithOne(o => o.Review)
            .HasForeignKey<Review>(r => r.OrderId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.FoodDrop)
            .WithMany(f => f.Messages)
            .HasForeignKey(m => m.FoodDropId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.FoodDrop)
            .WithMany(fd => fd.Favorites)
            .HasForeignKey(f => f.FoodDropId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.FoodDropId })
            .IsUnique();

        // Money columns: SQLite has no native decimal type; EF stores decimal
        // as TEXT by default which round-trips exactly (unlike double), so no
        // explicit column type is needed here — left as a note for the future
        // SQL Server migration, where these should map to decimal(10,2).
    }
}
