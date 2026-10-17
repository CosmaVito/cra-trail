using CvdInbox.Data;
using CvdInbox.Operators;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    private readonly bool retentionSweep;

    private readonly Action<IServiceCollection>? testServices;

    // Owned by the test, not by the app's services: its records outlive a host that was disposed after a failed start.
    private readonly FakeLogCollector logs = new();

    // The background retention sweep runs only when a test asks for it: its sweeps and log truncates would race the test's own steps.
    // testServices runs last, so a test can replace any service of the app.
    public InboxApp(IReadOnlyDictionary<string, string?>? overrides = null, bool retentionSweep = false, Action<IServiceCollection>? testServices = null)
    {
        this.retentionSweep = retentionSweep;
        this.testServices = testServices;
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

    public IReadOnlyList<FakeLogRecord> Logs() => logs.GetSnapshot();

    public IReadOnlyList<FakeLogRecord> SecurityEvents() =>
        [.. Logs().Where(record => record.Category == CvdInbox.Security.SecurityEvents.Category)];

    // Starts an app that should not start, and returns why. A validation error inside RunAsync disposes the host, and the
    // factory may then report the disposed host instead of the error; the host logs the error first, so the record has it.
    public Exception? StartupError()
    {
        var thrown = Record.Exception(() => CreateBrowser());
        return Logs().LastOrDefault(record => record.Id.Name == "HostedServiceStartupFaulted")?.Exception ?? thrown;
    }

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
            services.AddSingleton(logs);
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter, TestPeerAddress>();
            if (!retentionSweep)
                services.Remove(services.Single(d => !d.IsKeyedService && d.ImplementationType == typeof(CvdInbox.Retention.RetentionService)));
            testServices?.Invoke(services);
        });
    }

    // A host that fails to start is not kept by the factory, and Program.cs does not dispose it on an early failure.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        try
        {
            host.Start();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Pooled connections keep the files open on Windows. Only this app's pool: closing the last connection of
        // another app checkpoints its log, under a test that may be reading the log's bytes.
        using var connection = new SqliteConnection(DataSetup.ConnectionString(Path.Combine(DataDirectory, DataSetup.DatabaseFile)));
        SqliteConnection.ClearPool(connection);
        Directory.Delete(DataDirectory, recursive: true);
    }
}
