using CvdInbox.Data;
using CvdInbox.Operators;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace CvdInbox.Tests;

// One app per test: its own data folder, a fake clock and captured logs. Settings go through UseSetting,
// which minimal hosting reads before Program.cs builds the app.
public sealed class InboxApp : WebApplicationFactory<Program>
{
    public const string AlicePassword = "correct horse battery staple";

    private readonly Dictionary<string, string?> settings;

    public InboxApp(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        DataDirectory = Directory.CreateTempSubdirectory("cvd-inbox-").FullName;
        settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inbox:DataPath"] = DataDirectory,
            ["Disclosure:PublicOrigin"] = "https://inbox.example.test",
            ["Disclosure:Contact:0"] = "mailto:security@example.test",
            ["Disclosure:Expires"] = "2027-06-30T00:00:00Z",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            settings[key] = value;
    }

    public string DataDirectory { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

    public HttpClient CreateBrowser(string? peer = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (peer is not null)
            client.DefaultRequestHeaders.Add(TestPeerAddress.Header, peer);
        return client;
    }

    public IReadOnlyList<FakeLogRecord> Logs() => Services.GetFakeLogCollector().GetSnapshot();

    public IReadOnlyList<FakeLogRecord> SecurityEvents() =>
        [.. Logs().Where(record => record.Category == CvdInbox.Security.SecurityEvents.Category)];

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

    public Task<OperatorCreation> CreateOperatorAsync(string name = "alice", string password = AlicePassword) =>
        InScopeAsync(services => services.GetRequiredService<OperatorAccounts>().CreateAsync(name, password, CancellationToken.None));

    public Task<PasswordCheck> CheckPasswordAsync(string name, string password) =>
        InScopeAsync(services => services.GetRequiredService<OperatorAccounts>().CheckPasswordAsync(name, password, CancellationToken.None));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);
        builder.ConfigureLogging(logging => logging.AddFakeLogging());
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter, TestPeerAddress>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Pooled connections keep the files open on Windows.
        SqliteConnection.ClearAllPools();
        Directory.Delete(DataDirectory, recursive: true);
    }
}
