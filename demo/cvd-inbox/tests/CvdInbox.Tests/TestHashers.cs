using CvdInbox.Data;
using Microsoft.AspNetCore.Identity;

namespace CvdInbox.Tests;

// Counts the calls that reach the app's hasher.
internal sealed class CountingHasher : IPasswordHasher<Operator>
{
    private readonly PasswordHasher<Operator> inner = new();
    private int hashes;
    private int verifications;

    public int Hashes => Volatile.Read(ref hashes);

    public int Verifications => Volatile.Read(ref verifications);

    public string HashPassword(Operator user, string password)
    {
        Interlocked.Increment(ref hashes);
        return inner.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(Operator user, string hashedPassword, string providedPassword)
    {
        Interlocked.Increment(ref verifications);
        return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

// Holds the check of a real operator's password until the test releases it. The stand-in for unknown names has an empty name.
internal sealed class GateHasher : IPasswordHasher<Operator>
{
    private readonly PasswordHasher<Operator> inner = new();

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string HashPassword(Operator user, string password) => inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(Operator user, string hashedPassword, string providedPassword)
    {
        if (user.Name.Length > 0)
        {
            Entered.TrySetResult();
            Released.Task.Wait(TimeSpan.FromSeconds(10));
        }
        return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
