using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(220);
        builder.Property(x => x.Price).HasColumnType("numeric(10,2)");
        builder.Property(x => x.ImageUrl).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("idx_products_slug");
        builder.HasIndex(x => x.CategoryId).HasDatabaseName("idx_products_category_id");

        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId);

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
