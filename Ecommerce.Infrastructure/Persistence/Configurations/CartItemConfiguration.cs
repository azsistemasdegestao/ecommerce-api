using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");

        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(10,2)");

        builder.HasIndex(x => x.CartId).HasDatabaseName("idx_cart_items_cart_id");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_cart_items_product_id");

        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId);

        builder.Ignore(x => x.Subtotal);
    }
}
