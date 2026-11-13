using CryptoStuff.Core.Watchlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoStuff.Persistence.Watchlists;

internal sealed class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    // The backing field behind Watchlist.CoinIds, which is a read-only view.
    private const string CoinIdsField = "_coinIds";

    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("Watchlists");

        // An INTEGER PRIMARY KEY that SQLite generates with AUTOINCREMENT, so an
        // id is never handed out again after its watchlist is deleted.
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedOnAdd();

        // NOCASE on the column makes both the unique index and every name
        // comparison case-insensitive, matching NameExistsAsync's contract.
        builder.Property(w => w.Name).UseCollation("NOCASE");
        builder.HasIndex(w => w.Name).IsUnique();

        // The domain bumps Version on every accepted change to the name or the
        // coins, so as a token it turns any concurrent write into a conflict
        // instead of letting the last save overwrite the whole coin list.
        // SQLite has no row-version type, so EF only compares the value.
        builder.Property(w => w.Version).IsConcurrencyToken();

        // The ordered coin ids as one JSON array column, read and written
        // through the backing field so EF never needs a setter on the entity.
        builder.Ignore(w => w.CoinIds);
        builder.PrimitiveCollection<List<string>>(CoinIdsField)
            .HasColumnName(nameof(Watchlist.CoinIds))
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }
}
