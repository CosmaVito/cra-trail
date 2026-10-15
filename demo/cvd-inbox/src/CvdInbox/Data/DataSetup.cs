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
        {
            var inbox = provider.GetRequiredService<IOptions<InboxOptions>>().Value;
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var connection = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(inbox.DataDirectory(environment), DatabaseFile),
                Mode = SqliteOpenMode.ReadWriteCreate,
            };
            options.UseSqlite(connection.ToString())
                .AddInterceptors(provider.GetRequiredService<SqliteHardening>());
        });
        return services;
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
