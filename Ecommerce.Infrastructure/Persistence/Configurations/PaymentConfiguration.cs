using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(10,2)");
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_payments_order_id");

        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
    }
}
