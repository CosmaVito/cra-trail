using System.Security.Cryptography;
using CvdInbox.Data;
using CvdInbox.Security;

namespace CvdInbox.Operators;

// cvd-inbox create-operator <name> [--password-stdin]
// cvd-inbox remove-operator <name>
// There is no setup page and no default account: operators are created and removed on the server, by someone with a shell there.
public static class OperatorCommand
{
    public const string Create = "create-operator";
    public const string Remove = "remove-operator";
    private const string PasswordStdin = "--password-stdin";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public const string Usage = $"Usage: cvd-inbox {Create} <name> [{PasswordStdin}] | cvd-inbox {Remove} <name>";

    public static bool IsCommand(string[] args) => args is [Create or Remove, ..];

    // Configuration arguments look like --key value, --key=value, /key value or key=value. Any other first word
    // is a command, and one this app does not know must not start the server.
    public static bool IsUnknownCommand(string[] args) =>
        args is [var first, ..] && !IsCommand(args) && !first.StartsWith('-') && !first.StartsWith('/') && !first.Contains('=', StringComparison.Ordinal);

    // Exit codes: 0 done, 1 refused or failed, 2 wrong usage.
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        try
        {
            switch (args)
            {
                // A name that starts with '-' is an option in the wrong place, such as "create-operator --password-stdin".
                case [Create, var name] when !name.StartsWith('-'):
                    return await CreateAsync(services, name, fromStdin: false, input, output, error);
                case [Create, var name, PasswordStdin] when !name.StartsWith('-'):
                    return await CreateAsync(services, name, fromStdin: true, input, output, error);
                case [Remove, var name] when !name.StartsWith('-'):
                    return await RemoveAsync(services, name, output, error);
                default:
                    await error.WriteLineAsync(Usage);
                    return 2;
            }
        }
        catch (Exception e)
        {
            await error.WriteLineAsync($"error: {e.Message}");
            return 1;
        }
    }

    private static async Task<int> CreateAsync(IServiceProvider services, string name, bool fromStdin, TextReader input, TextWriter output, TextWriter error)
    {
        var password = fromStdin
            ? (await input.ReadLineAsync())?.TrimEnd('\r') ?? ""
            : RandomNumberGenerator.GetString(Alphabet, 24);

        await services.PrepareDatabaseAsync();
        await using var scope = services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<OperatorAccounts>().CreateAsync(name, password, CancellationToken.None);
        if (!created.Succeeded)
        {
            await error.WriteLineAsync(created.Error);
            return 1;
        }

        services.GetRequiredService<SecurityEvents>().OperatorCreated(created.Name!);
        // A relative Inbox:DataPath starts at the content root, which is the app's folder.
        // The path shows which database got the operator.
        await output.WriteLineAsync($"Created operator {created.Name} in {services.DatabasePath()}.");
        if (!fromStdin)
        {
            await output.WriteLineAsync($"Password: {password}");
            await output.WriteLineAsync("It is shown only once. Keep it in a password manager.");
        }
        return 0;
    }

    private static async Task<int> RemoveAsync(IServiceProvider services, string name, TextWriter output, TextWriter error)
    {
        await services.PrepareDatabaseAsync();
        await using var scope = services.CreateAsyncScope();
        var removed = await scope.ServiceProvider.GetRequiredService<OperatorAccounts>().RemoveAsync(name, CancellationToken.None);
        if (removed is null)
        {
            await error.WriteLineAsync($"No operator {name} in {services.DatabasePath()}.");
            return 1;
        }

        // secure_delete has zeroed the row in the database file; the truncate takes it out of the write-ahead log.
        var events = services.GetRequiredService<SecurityEvents>();
        if (!await scope.ServiceProvider.GetRequiredService<InboxDb>().TruncateWalAsync(CancellationToken.None))
            events.WalTruncateIncomplete();
        events.OperatorRemoved(removed);
        await output.WriteLineAsync($"Removed operator {removed} from {services.DatabasePath()}. Their sessions have ended.");
        return 0;
    }
}
