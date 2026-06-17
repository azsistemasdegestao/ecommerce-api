using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(10,2)");

        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_items_order_id");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_order_items_product_id");

        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId);

        builder.Ignore(x => x.Subtotal);
    }
}
