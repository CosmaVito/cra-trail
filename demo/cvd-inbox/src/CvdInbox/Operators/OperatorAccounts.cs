using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CvdInbox.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Operators;

public enum SignInOutcome
{
    Succeeded,
    Failed,
    LockedOut,
}

public sealed record PasswordCheck(SignInOutcome Outcome, string? Name);

public sealed record OperatorCreation(string? Name, string? Error)
{
    public bool Succeeded => Error is null;
}

public sealed partial class OperatorAccounts(InboxDb db, IPasswordHasher<Operator> hasher, TimeProvider time)
{
    // NIST SP 800-63B-4, 3.1.1.2: at least 15 characters when the password is the only factor.
    public const int MinimumPasswordLength = 15;
    public const int MaximumPasswordLength = 128;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private static readonly Operator Nobody = new() { Name = "", PasswordHash = "" };
    private static readonly string NobodyHash =
        new PasswordHasher<Operator>().HashPassword(Nobody, Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)));

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,31}$")]
    private static partial Regex NamePattern();

    public static string? NormalizeName(string? name)
    {
        var normalized = name?.Trim().ToLowerInvariant();
        return normalized is not null && NamePattern().IsMatch(normalized) ? normalized : null;
    }

    public async Task<OperatorCreation> CreateAsync(string name, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        if (normalized is null)
            return new(null, "Operator names are 2 to 32 characters: lower-case letters, digits, '.', '_' or '-'.");
        if (password.Length is < MinimumPasswordLength or > MaximumPasswordLength)
            return new(null, $"Passwords need {MinimumPasswordLength} to {MaximumPasswordLength} characters.");
        if (await db.Operators.AnyAsync(o => o.Name == normalized, cancellationToken))
            return new(null, $"Operator {normalized} already exists.");

        var account = new Operator { Name = normalized, PasswordHash = "", CreatedAt = time.GetUtcNow().UtcDateTime };
        account.PasswordHash = hasher.HashPassword(account, password);
        db.Operators.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return new(normalized, null);
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
            hasher.VerifyHashedPassword(Nobody, NobodyHash, password ?? "");
            return new(account is null ? SignInOutcome.Failed : SignInOutcome.LockedOut, null);
        }

        var result = hasher.VerifyHashedPassword(account, account.PasswordHash, password ?? "");
        if (result == PasswordVerificationResult.Failed)
        {
            account.FailedSignIns++;
            var lockedOut = account.FailedSignIns >= MaxFailedSignIns;
            if (lockedOut)
            {
                account.FailedSignIns = 0;
                account.LockedUntil = now + LockoutDuration;
            }
            await db.SaveChangesAsync(cancellationToken);
            return new(lockedOut ? SignInOutcome.LockedOut : SignInOutcome.Failed, null);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = hasher.HashPassword(account, password!);
        account.FailedSignIns = 0;
        account.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);
        return new(SignInOutcome.Succeeded, account.Name);
    }
}

public static class OperatorSetup
{
    public static IServiceCollection AddOperators(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<Operator>, PasswordHasher<Operator>>();
        services.AddScoped<OperatorAccounts>();
        return services;
    }
}
