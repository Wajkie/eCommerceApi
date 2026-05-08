using Microsoft.EntityFrameworkCore;
using eCommerceApi.Models;

namespace eCommerceApi.Data
{
    public class EcommerceContext : DbContext
    {
        public EcommerceContext(DbContextOptions<EcommerceContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.StoreId, p.ExternalId })
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.StoreId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.StoreId);

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.StoreId, o.IdempotencyKey })
                .IsUnique();

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.StoreId);

            modelBuilder.Entity<OrderItem>()
                .HasIndex(oi => new { oi.OrderId, oi.ProductId });

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.CustomerId);

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.Status);

            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => ci.CartId);

            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), StoreId = Guid.Empty, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
        }
    }
}
