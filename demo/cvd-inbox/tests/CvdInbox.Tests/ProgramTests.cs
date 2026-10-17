using System.Diagnostics;
using CvdInbox.Data;

namespace CvdInbox.Tests;

// Starts the built app as its own process, the way the zip and the image run it. The build copies cvd-inbox.dll,
// its runtimeconfig and appsettings.json into the test output folder.
public class ProgramTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unknown_command_prints_the_usage_and_starts_no_server()
    {
        var data = Directory.CreateTempSubdirectory("cvd-inbox-").FullName;
        try
        {
            // Settings the server would start with: before the check, "foo" was ignored and the server kept running.
            var (exit, output, error) = await RunAsync(["foo"], Path.GetTempPath(), new()
            {
                ["Inbox__DataPath"] = data,
                ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
                ["Disclosure__PublicOrigin"] = "https://inbox.example.test",
                ["Disclosure__Expires"] = "2099-06-30T00:00:00Z",
            });

            Assert.Equal(2, exit);
            Assert.StartsWith("Usage: cvd-inbox", error, StringComparison.Ordinal);
            Assert.Empty(output);
            Assert.Empty(Directory.EnumerateFileSystemEntries(data));
        }
        finally
        {
            Directory.Delete(data, recursive: true);
        }
    }

    [Fact]
    public async Task A_relative_data_path_starts_at_the_app_folder_whatever_the_working_directory()
    {
        var folder = $"zz-data-{Guid.NewGuid():N}";
        var workingDirectory = Directory.CreateTempSubdirectory("cvd-inbox-cwd-").FullName;
        try
        {
            var (exit, output, _) = await RunAsync(["create-operator", "alice", "--password-stdin"], workingDirectory,
                new() { ["Inbox__DataPath"] = folder }, InboxApp.AlicePassword + "\n");

            Assert.Equal(0, exit);
            var created = output.Split('\n').Single(line => line.StartsWith("Created operator", StringComparison.Ordinal)).Trim();
            Assert.Equal($"Created operator alice in {Path.Combine(AppContext.BaseDirectory, folder, DataSetup.DatabaseFile)}.", created);
            Assert.Empty(Directory.EnumerateFileSystemEntries(workingDirectory));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
            var data = Path.Combine(AppContext.BaseDirectory, folder);
            if (Directory.Exists(data))
                Directory.Delete(data, recursive: true);
        }
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(
        string[] args, string workingDirectory, Dictionary<string, string> environment, string input = "")
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "cvd-inbox.dll"));
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        foreach (var (key, value) in environment)
            start.Environment[key] = value;

        using var process = Process.Start(start)!;
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(Ct);
        var error = process.StandardError.ReadToEndAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"cvd-inbox {string.Join(' ', args)} was still running after 30 seconds.");
        }
        return (process.ExitCode, await output, await error);
    }
}
