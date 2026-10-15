using CvdInbox.Data;
using CvdInbox.Operators;

namespace CvdInbox.Tests;

public class OperatorCommandTests
{
    [Fact]
    public async Task Creates_an_operator_with_a_password_from_stdin()
    {
        await using var app = new InboxApp();

        var (exit, output, _) = await RunAsync(app, ["create-operator", "alice", "--password-stdin"], InboxApp.AlicePassword + "\n");

        Assert.Equal(0, exit);
        // The path tells which database got the operator: a relative data path depends on the working directory.
        Assert.Equal($"Created operator alice in {Path.Combine(app.DataDirectory, DataSetup.DatabaseFile)}.", output.Trim());
        Assert.Equal(SignInOutcome.Succeeded, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1005);
    }

    [Fact]
    public async Task Without_stdin_it_generates_a_password_and_shows_it_once()
    {
        await using var app = new InboxApp();

        var (exit, output, _) = await RunAsync(app, ["create-operator", "bob"], "");

        Assert.Equal(0, exit);
        var line = output.Split('\n').Single(l => l.StartsWith("Password: ", StringComparison.Ordinal));
        var password = line["Password: ".Length..].Trim();
        Assert.Equal(24, password.Length);
        Assert.Equal(SignInOutcome.Succeeded, (await app.CheckPasswordAsync("bob", password)).Outcome);
    }

    [Theory]
    [InlineData(2, "create-operator")]
    [InlineData(2, "create-operator", "alice", "--password")]
    [InlineData(1, "create-operator", "alice", "--password-stdin")]
    public async Task Exit_codes_tell_usage_errors_from_refusals(int expected, params string[] args)
    {
        await using var app = new InboxApp();

        var (exit, _, error) = await RunAsync(app, args, "too short\n");

        Assert.Equal(expected, exit);
        Assert.NotEmpty(error);
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(InboxApp app, string[] args, string input)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await OperatorCommand.RunAsync(app.Services, args, new StringReader(input), output, error);
        return (exit, output.ToString(), error.ToString());
    }
}
