using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CvdInbox.Data;

public static class DataSetup
{
    public const string DatabaseFile = "cvd-inbox.db";

    public static IServiceCollection AddInboxData(this IServiceCollection services)
    {
        services.AddSingleton<SqliteHardening>();
        services.AddDbContext<InboxDb>((provider, options) =>
            options.UseSqlite(ConnectionString(provider.DatabasePath()))
                .AddInterceptors(provider.GetRequiredService<SqliteHardening>()));
        return services;
    }

    // Also the key of the connection pool, which the tests clear for one app at a time.
    public static string ConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();

    public static string DatabasePath(this IServiceProvider services)
    {
        var inbox = services.GetRequiredService<IOptions<InboxOptions>>().Value;
        return Path.Combine(inbox.DataDirectory(services.GetRequiredService<IHostEnvironment>()), DatabaseFile);
    }

    public static async Task PrepareDatabaseAsync(this IServiceProvider services)
    {
        var inbox = services.GetRequiredService<IOptions<InboxOptions>>().Value;
        var directory = inbox.DataDirectory(services.GetRequiredService<IHostEnvironment>());
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InboxDb>().Database.MigrateAsync();
    }
}
