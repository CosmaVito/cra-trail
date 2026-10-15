using CvdInbox.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CvdInbox.Tests;

// One app per test: its own data folder and a fake clock. Settings go through UseSetting, which
// minimal hosting reads before Program.cs builds the app.
public sealed class InboxApp : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> settings;

    public InboxApp(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        DataDirectory = Directory.CreateTempSubdirectory("cvd-inbox-").FullName;
        settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inbox:DataPath"] = DataDirectory,
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            settings[key] = value;
    }

    public string DataDirectory { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task<T> WithDbAsync<T>(Func<InboxDb, Task<T>> action) =>
        InScopeAsync(services => action(services.GetRequiredService<InboxDb>()));

    public Task<int> AddReportAsync(Report report) => WithDbAsync(async db =>
    {
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Time));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Pooled connections keep the files open on Windows.
        SqliteConnection.ClearAllPools();
        Directory.Delete(DataDirectory, recursive: true);
    }
}
