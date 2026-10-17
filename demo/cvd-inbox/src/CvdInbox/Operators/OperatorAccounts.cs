using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CvdInbox.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Operators;

public enum SignInOutcome
{
    Succeeded,
    Failed,
    LockedOut,
}

// On success, Name and the operator's current security stamp, which goes into the session cookie.
public sealed record PasswordCheck(SignInOutcome Outcome, string? Name, string? Stamp = null);

public sealed record OperatorCreation(string? Name, string? Error)
{
    public bool Succeeded => Error is null;
}

// What unknown names and locked accounts are checked against. The app's own hasher makes it, so the check costs
// what a real one costs, whatever the hasher's settings.
public sealed class NobodyPassword(IPasswordHasher<Operator> hasher)
{
    public static readonly Operator Nobody = new() { Name = "", PasswordHash = "", SecurityStamp = "" };

    public string Hash { get; } = hasher.HashPassword(Nobody, Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)));
}

public sealed partial class OperatorAccounts(InboxDb db, IPasswordHasher<Operator> hasher, NobodyPassword nobody, TimeProvider time)
{
    // NIST SP 800-63B-4, 3.1.1.2: at least 15 characters when the password is the only factor.
    public const int MinimumPasswordLength = 15;
    public const int MaximumPasswordLength = 128;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static string NewStamp() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,31}$")]
    private static partial Regex NamePattern();

    public static string? NormalizeName(string? name)
    {
        // ASCII is checked before lower-casing, which turns U+212A KELVIN SIGN into an ASCII k.
        var trimmed = name?.Trim();
        if (trimmed is null || !Ascii.IsValid(trimmed))
            return null;
        var normalized = trimmed.ToLowerInvariant();
        return NamePattern().IsMatch(normalized) ? normalized : null;
    }

    public async Task<OperatorCreation> CreateAsync(string name, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        if (normalized is null)
            return new(null, "Operator names are 2 to 32 characters: lower-case letters, digits, '.', '_' or '-', starting with a letter or a digit.");
        // NIST SP 800-63B-4, 3.1.1.2: each Unicode code point counts as one character. There is no NFC normalization:
        // with InvariantGlobalization, string.Normalize returns the string unchanged.
        if (password.EnumerateRunes().Count() is < MinimumPasswordLength or > MaximumPasswordLength)
            return new(null, $"Passwords need {MinimumPasswordLength} to {MaximumPasswordLength} characters.");
        if (await db.Operators.AnyAsync(o => o.Name == normalized, cancellationToken))
            return new(null, $"Operator {normalized} already exists.");

        var account = new Operator { Name = normalized, PasswordHash = "", CreatedAt = time.GetUtcNow().UtcDateTime, SecurityStamp = NewStamp() };
        account.PasswordHash = hasher.HashPassword(account, password);
        db.Operators.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // Another create of the same name got in while the hash ran. 19 is SQLITE_CONSTRAINT, from the unique name index.
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            return new(null, $"Operator {normalized} already exists.");
        }
        return new(normalized, null);
    }

    // Removes the operator; their sessions fail the stamp check at the next request. The normalized name if a row went, otherwise null.
    public async Task<string?> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        return normalized is not null && await db.Operators.Where(o => o.Name == normalized).ExecuteDeleteAsync(cancellationToken) > 0
            ? normalized
            : null;
    }

    public async Task<bool> IsSessionValidAsync(string? name, string? stamp, CancellationToken cancellationToken) =>
        name is not null && stamp is not null
        && await db.Operators.AnyAsync(o => o.Name == name && o.SecurityStamp == stamp, cancellationToken);

    // A new stamp ends every session of the operator, on every device. It is written even if the client goes away.
    public async Task EndSessionsAsync(string name)
    {
        var stamp = NewStamp();
        await db.Operators.Where(o => o.Name == name)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.SecurityStamp, stamp), CancellationToken.None);
    }

    // The normalized name when an operator has it, otherwise null.
    public async Task<string?> ExistingNameAsync(string? name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        return normalized is not null && await db.Operators.AnyAsync(o => o.Name == normalized, cancellationToken) ? normalized : null;
    }

    public async Task<PasswordCheck> CheckPasswordAsync(string? name, string? password, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var normalized = NormalizeName(name);
        var account = normalized is null
            ? null
            : await db.Operators.SingleOrDefaultAsync(o => o.Name == normalized, cancellationToken);

        // Unknown names and locked accounts cost a hash too, so response times do not tell which names exist.
        if (account is null || account.LockedUntil > now)
        {
            hasher.VerifyHashedPassword(NobodyPassword.Nobody, nobody.Hash, password ?? "");
            return new(account is null ? SignInOutcome.Failed : SignInOutcome.LockedOut, null);
        }

        var result = hasher.VerifyHashedPassword(account, account.PasswordHash, password ?? "");
        // The hash has been checked: its outcome is stored even if the client goes away now.
        if (result == PasswordVerificationResult.Failed)
        {
            // Counted in SQL: parallel guesses each load the same row while the hash runs, so a read-then-write would lose counts.
            var accountId = account.Id;
            await db.Operators.Where(o => o.Id == accountId)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.FailedSignIns, o => o.FailedSignIns + 1), CancellationToken.None);
            var lockedUntil = now + LockoutDuration;
            var lockedOut = await db.Operators.Where(o => o.Id == accountId && o.FailedSignIns >= MaxFailedSignIns)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(o => o.FailedSignIns, 0)
                    .SetProperty(o => o.LockedUntil, lockedUntil), CancellationToken.None) > 0;
            return new(lockedOut ? SignInOutcome.LockedOut : SignInOutcome.Failed, null);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = hasher.HashPassword(account, password!);
        account.FailedSignIns = 0;
        account.LockedUntil = null;
        await db.SaveChangesAsync(CancellationToken.None);
        return new(SignInOutcome.Succeeded, account.Name, account.SecurityStamp);
    }
}

public static class OperatorSetup
{
    public static IServiceCollection AddOperators(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<Operator>, PasswordHasher<Operator>>();
        services.AddSingleton<NobodyPassword>();
        services.AddScoped<OperatorAccounts>();
        return services;
    }
}
