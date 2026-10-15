using CvdInbox.Data;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task Startup_creates_the_database_in_the_data_folder()
    {
        await using var app = new InboxApp();

        _ = app.CreateBrowser();

        Assert.True(File.Exists(Path.Combine(app.DataDirectory, DataSetup.DatabaseFile)));
    }

    [Fact]
    public async Task Every_connection_zeroes_deleted_content_keeps_temporary_data_in_memory_and_enforces_foreign_keys()
    {
        await using var app = new InboxApp();

        var (secureDelete, foreignKeys, tempStore) = await app.WithDbAsync(async db =>
        {
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA secure_delete;";
            var secure = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            command.CommandText = "PRAGMA foreign_keys;";
            var keys = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            command.CommandText = "PRAGMA temp_store;";
            var temp = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            return (secure, keys, temp);
        });

        Assert.Equal(1L, secureDelete);
        Assert.Equal(1L, foreignKeys);
        // 2 is MEMORY.
        Assert.Equal(2L, tempStore);
    }

    [Fact]
    public async Task Times_come_back_as_utc()
    {
        await using var app = new InboxApp();
        var received = new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc);
        await app.AddReportAsync(TestData.Report(received));

        var stored = await app.WithDbAsync(db => db.Reports.Select(r => r.ReceivedAt).SingleAsync());

        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(received, stored);
    }
}
