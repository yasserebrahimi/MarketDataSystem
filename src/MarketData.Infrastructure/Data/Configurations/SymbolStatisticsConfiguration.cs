using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketData.Infrastructure.Data.Configurations;

public class SymbolStatisticsConfiguration : IEntityTypeConfiguration<SymbolStatistics>
{
    public void Configure(EntityTypeBuilder<SymbolStatistics> builder)
    {
        builder.ToTable("SymbolStatistics");

        builder.HasKey(s => s.Symbol);

        builder.Property(s => s.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(s => s.CurrentPrice)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(s => s.MovingAverage)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(s => s.MinPrice)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(s => s.MaxPrice)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(s => s.UpdateCount)
            .IsRequired();

        builder.Property(s => s.LastUpdateTime)
            .IsRequired();

        // Index for performance
        builder.HasIndex(s => s.LastUpdateTime);
    }
}
