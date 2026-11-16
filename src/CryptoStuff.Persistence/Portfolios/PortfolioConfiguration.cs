using CryptoStuff.Core.Portfolios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoStuff.Persistence.Portfolios;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    // The backing field behind Portfolio.Transactions, which is a read-only copy.
    private const string TransactionsField = "_transactions";

    // The portfolio's highest sequence ever handed out, which only the domain reads.
    private const string LastSequenceField = "_lastSequence";

    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        // The collation on the column makes both the unique index and every
        // name comparison case-insensitive across Unicode, matching
        // NameExistsAsync's contract; SQLite's NOCASE folds only ASCII.
        builder.Property(p => p.Name).UseCollation(UnicodeNoCaseCollation.Name);
        builder.HasIndex(p => p.Name).IsUnique();

        // The domain bumps Version on every accepted change to the portfolio or
        // its ledger, so as a token it turns any concurrent write into a conflict.
        // SQLite has no row-version type, so EF only compares the value.
        builder.Property(p => p.Version).IsConcurrencyToken();

        // A get-only property is mapped only when named, then through its backing field.
        builder.Property(p => p.VsCurrency);

        // Stored so that a removed newest transaction's sequence stays spent
        // across loads, not just within one unit of work.
        builder.Property<int>(LastSequenceField).HasColumnName("LastSequence");

        builder.Property(p => p.CostBasisMethod)
            .HasConversion(TokenConverter.For<CostBasisMethod>(CostBasisMethodParser.TryParse, "cost-basis method"));

        builder.Ignore(p => p.Transactions);
        // Owned, so a load always brings the whole ledger with the portfolio, and
        // ownership makes the link a required foreign key that cascades on delete.
        builder.OwnsMany<PortfolioTransaction>(TransactionsField, transactions =>
        {
            transactions.ToTable("PortfolioTransactions");

            // Its own single-column key, so SQLite generates it with AUTOINCREMENT.
            transactions.HasKey(t => t.Id);
            transactions.Property(t => t.Id).ValueGeneratedOnAdd();

            // SQLite has no decimal type; TEXT keeps the digits exact. Nothing
            // orders or sums these columns in SQL, so their text order is moot.
            transactions.Property(t => t.Quantity).HasColumnType("TEXT");
            transactions.Property(t => t.UnitPrice).HasColumnType("TEXT");
            transactions.Property(t => t.Fee).HasColumnType("TEXT");

            // Stored as the UTC date and time without an offset, which SQLite
            // reads back as unspecified and so is stamped UTC again here.
            transactions.Property(t => t.OccurredAt).HasConversion(
                occurredAt => occurredAt.UtcDateTime,
                utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

            transactions.Property(t => t.Kind)
                .HasConversion(TokenConverter.For<TransactionKind>(TransactionKindParser.TryParse, "transaction kind"));

            // The domain never hands a portfolio's sequence out twice; the index
            // holds any other writer to the same rule. It leads with the foreign
            // key, so it also serves the lookups that key's own index did.
            transactions.HasIndex("PortfolioId", nameof(PortfolioTransaction.Sequence)).IsUnique();
        });
    }
}
