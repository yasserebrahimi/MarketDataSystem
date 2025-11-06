using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketData.Infrastructure.Data.Configurations;

public class PriceAnomalyConfiguration : IEntityTypeConfiguration<PriceAnomaly>
{
    public void Configure(EntityTypeBuilder<PriceAnomaly> builder)
    {
        builder.ToTable("PriceAnomalies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(a => a.OldPrice)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(a => a.NewPrice)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(a => a.ChangePercent)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(a => a.DetectedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasMaxLength(20);

        // Indexes for performance
        builder.HasIndex(a => a.Symbol);
        builder.HasIndex(a => a.DetectedAt);
        builder.HasIndex(a => a.Severity);
        builder.HasIndex(a => new { a.Symbol, a.DetectedAt });
    }
}
