using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketData.Infrastructure.Data.Configurations;

public class PriceUpdateConfiguration : IEntityTypeConfiguration<PriceUpdate>
{
    public void Configure(EntityTypeBuilder<PriceUpdate> builder)
    {
        builder.ToTable("PriceUpdates");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(p => p.Timestamp)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes for performance
        builder.HasIndex(p => p.Symbol);
        builder.HasIndex(p => p.Timestamp);
        builder.HasIndex(p => new { p.Symbol, p.Timestamp });
    }
}
