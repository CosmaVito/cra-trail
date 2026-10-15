using System.Security.Cryptography;
using CvdInbox.Data;
using CvdInbox.Security;

namespace CvdInbox.Operators;

// cvd-inbox create-operator <name> [--password-stdin]
// There is no setup page and no default account: operators are created on the server, by someone with a shell there.
public static class OperatorCommand
{
    public const string Name = "create-operator";
    private const string PasswordStdin = "--password-stdin";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args is not [Name, var name, .. var options] || options.Any(option => option != PasswordStdin))
        {
            await error.WriteLineAsync($"Usage: cvd-inbox {Name} <name> [{PasswordStdin}]");
            return 2;
        }

        var fromStdin = options.Contains(PasswordStdin);
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
        // A relative Inbox:DataPath starts at the content root, which is the working directory.
        // The path shows which database got the operator.
        await output.WriteLineAsync($"Created operator {created.Name} in {services.DatabasePath()}.");
        if (!fromStdin)
        {
            await output.WriteLineAsync($"Password: {password}");
            await output.WriteLineAsync("It is shown only once. Keep it in a password manager.");
        }
        return 0;
    }
}
