using CryptoStuff.Core.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CryptoStuff.Persistence.Alerts;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<PriceAlert>
{
    public void Configure(EntityTypeBuilder<PriceAlert> builder)
    {
        builder.ToTable("PriceAlerts");

        // An INTEGER PRIMARY KEY that SQLite generates with AUTOINCREMENT, so an
        // id is never handed out again after its alert is deleted.
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        // The domain bumps Version on every accepted change, so as a token it
        // turns any concurrent write into a conflict. SQLite has no row-version
        // type, so EF only compares the value.
        builder.Property(a => a.Version).IsConcurrencyToken();

        // Get-only properties are mapped only when named, then through their
        // backing fields; EF binds them to PriceAlert's private constructor.
        builder.Property(a => a.CoinId);
        builder.Property(a => a.VsCurrency);

        // SQLite has no decimal type; TEXT keeps the digits exact. Nothing orders
        // or compares these columns in SQL, so their text order is moot.
        builder.Property(a => a.Threshold).HasColumnType("TEXT");
        builder.Property(a => a.TriggeredPrice).HasColumnType("TEXT");

        builder.Property(a => a.Direction)
            .HasConversion(direction => AlertTokens.ToToken(direction), token => AlertTokens.ToDirection(token));

        builder.Property(a => a.State)
            .HasConversion(state => AlertTokens.ToToken(state), token => AlertTokens.ToState(token));

        // Stored as the UTC date and time without an offset, which SQLite reads
        // back as unspecified and so is stamped UTC again here. The converter
        // is never handed the null of an armed alert.
        builder.Property(a => a.TriggeredAt).HasConversion(new ValueConverter<DateTimeOffset, DateTime>(
            triggeredAt => triggeredAt.UtcDateTime,
            utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))));
    }
}
