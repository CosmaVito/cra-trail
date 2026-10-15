using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CvdInbox.Data;

public sealed class InboxDb(DbContextOptions<InboxDb> options) : DbContext(options)
{
    // SQLite has no date type and EF Core reads DateTime back as Unspecified. Every stored time is UTC.
    private static readonly ValueConverter<DateTime, DateTime> Utc =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Operator> Operators => Set<Operator>();

    // Copies the write-ahead log into the database and truncates it, so erased values do not linger there.
    public Task TruncateWalAsync(CancellationToken cancellationToken) =>
        Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Report>(report =>
        {
            report.HasIndex(r => r.ReceiptHash).IsUnique();
            report.Property(r => r.ReceiptHash).HasMaxLength(64);
            report.Property(r => r.Product).HasMaxLength(100);
            report.Property(r => r.Version).HasMaxLength(50);
            report.Property(r => r.Description).HasMaxLength(20_000);
            report.Property(r => r.Contact).HasMaxLength(200);
            report.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            report.HasMany(r => r.Notes).WithOne().HasForeignKey(n => n.ReportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Note>(note =>
        {
            note.Property(n => n.Author).HasMaxLength(32);
            note.Property(n => n.Text).HasMaxLength(4_000);
        });
        modelBuilder.Entity<Operator>(account =>
        {
            account.HasIndex(o => o.Name).IsUnique();
            account.Property(o => o.Name).HasMaxLength(32);
        });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                property.SetValueConverter(Utc);
        }
    }
}
