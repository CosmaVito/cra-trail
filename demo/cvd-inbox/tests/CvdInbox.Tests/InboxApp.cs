using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
        Directory.Delete(DataDirectory, recursive: true);
    }
}
