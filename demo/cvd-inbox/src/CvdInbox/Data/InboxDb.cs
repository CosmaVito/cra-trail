using Microsoft.Data.Sqlite;
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
    // A reader that is still open keeps the checkpoint from finishing (busy = 1). It is retried briefly, then
    // given up; false means old pages may still sit in the log until a later truncate succeeds.
    public async Task<bool> TruncateWalAsync(CancellationToken cancellationToken)
    {
        const int attempts = 5;
        // A connection of its own with no busy timeout: the default one would wait for readers instead of reporting busy.
        var connectionString = new SqliteConnectionStringBuilder(Database.GetConnectionString()) { DefaultTimeout = 0, Pooling = false };
        await using var connection = new SqliteConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await using var row = await command.ExecuteReaderAsync(cancellationToken);
            if (await row.ReadAsync(cancellationToken) && row.GetInt64(0) == 0)
                return true;
            if (attempt < attempts)
                await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
        }
        return false;
    }

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
            account.Property(o => o.SecurityStamp).HasMaxLength(32);
        });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                property.SetValueConverter(Utc);
        }
    }
}
