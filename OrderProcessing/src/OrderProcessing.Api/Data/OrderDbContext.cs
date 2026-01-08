using Microsoft.EntityFrameworkCore;

namespace OrderProcessing.Api.Data;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.CustomerId).HasColumnName("customer_id");
            entity.Property(order => order.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(order => order.TotalAmount).HasColumnName("total_amount");
            entity.Property(order => order.Currency).HasColumnName("currency");
            entity.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId);
        });

        modelBuilder.Entity<OrderItemEntity>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrderId).HasColumnName("order_id");
            entity.Property(item => item.Sku).HasColumnName("sku");
            entity.Property(item => item.UnitPrice).HasColumnName("unit_price");
        });
    }
}
