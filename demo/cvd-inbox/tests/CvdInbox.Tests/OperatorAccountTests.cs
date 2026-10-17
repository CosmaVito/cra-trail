using CvdInbox.Data;
using CvdInbox.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvdInbox.Tests;

public class OperatorAccountTests
{
    private const string WrongPassword = "wrong password, long enough";

    [Fact]
    public async Task Creates_an_operator_with_a_hashed_password()
    {
        await using var app = new InboxApp();

        var created = await app.CreateOperatorAsync("Alice");

        Assert.True(created.Succeeded);
        Assert.Equal("alice", created.Name);
        var stored = await app.WithDbAsync(db => db.Operators.SingleAsync());
        Assert.DoesNotContain(InboxApp.AlicePassword, stored.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(SignInOutcome.Succeeded, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);
    }

    [Theory]
    [InlineData("a", InboxApp.AlicePassword, "names")]
    [InlineData("bad name", InboxApp.AlicePassword, "names")]
    [InlineData("alice", "fourteen chars", "15")]
    public async Task Refuses_bad_names_and_short_passwords(string name, string password, string hint)
    {
        await using var app = new InboxApp();

        var created = await app.CreateOperatorAsync(name, password);

        Assert.False(created.Succeeded);
        Assert.Contains(hint, created.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_are_unique_whatever_the_case()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync("alice");

        var again = await app.CreateOperatorAsync("ALICE");

        Assert.False(again.Succeeded);
        Assert.Contains("already exists", again.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync();

        for (var attempt = 1; attempt <= 4; attempt++)
            Assert.Equal(SignInOutcome.Failed, (await app.CheckPasswordAsync("alice", WrongPassword)).Outcome);
        Assert.Equal(SignInOutcome.LockedOut, (await app.CheckPasswordAsync("alice", WrongPassword)).Outcome);
        Assert.Equal(SignInOutcome.LockedOut, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);

        app.Time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        Assert.Equal(SignInOutcome.Succeeded, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);
    }

    [Fact]
    public async Task A_good_password_resets_the_count()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync();
        for (var attempt = 1; attempt <= 4; attempt++)
            await app.CheckPasswordAsync("alice", WrongPassword);
        await app.CheckPasswordAsync("alice", InboxApp.AlicePassword);

        for (var attempt = 1; attempt <= 4; attempt++)
            Assert.Equal(SignInOutcome.Failed, (await app.CheckPasswordAsync("alice", WrongPassword)).Outcome);
    }

    [Fact]
    public async Task Unknown_names_fail_like_wrong_passwords()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync();

        Assert.Equal(new PasswordCheck(SignInOutcome.Failed, null), await app.CheckPasswordAsync("nobody", InboxApp.AlicePassword));
        Assert.Equal(new PasswordCheck(SignInOutcome.Failed, null), await app.CheckPasswordAsync("alice", WrongPassword));
    }

    [Fact]
    public async Task Parallel_wrong_passwords_still_lock_the_account()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync();

        // Task.Run: the SQLite calls and the hash complete synchronously, so a bare WhenAll would run them one by one.
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => app.CheckPasswordAsync("alice", WrongPassword))));

        Assert.Equal(SignInOutcome.LockedOut, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);
    }

    [Fact]
    public async Task Unknown_names_and_locked_accounts_cost_one_hash_check_each()
    {
        var hasher = new CountingHasher();
        await using var app = new InboxApp(testServices: services => services.AddSingleton<IPasswordHasher<Operator>>(hasher));
        await app.CreateOperatorAsync();

        await app.CheckPasswordAsync("nobody", InboxApp.AlicePassword);
        Assert.Equal(1, hasher.Verifications);

        for (var attempt = 1; attempt <= 5; attempt++)
            await app.CheckPasswordAsync("alice", WrongPassword);
        Assert.Equal(SignInOutcome.LockedOut, (await app.CheckPasswordAsync("alice", InboxApp.AlicePassword)).Outcome);
        Assert.Equal(7, hasher.Verifications);
    }

    [Fact]
    public async Task The_stand_in_for_unknown_names_is_hashed_by_the_apps_own_hasher()
    {
        var hasher = new CountingHasher();
        await using var app = new InboxApp(testServices: services => services.AddSingleton<IPasswordHasher<Operator>>(hasher));
        await app.CreateOperatorAsync();

        await app.CheckPasswordAsync("nobody", InboxApp.AlicePassword);

        // One hash for alice, one for the stand-in: a hasher with other settings would make unknown names faster or slower.
        Assert.Equal(2, hasher.Hashes);
    }

    [Fact]
    public async Task Names_are_trimmed_and_lower_cased_at_sign_in()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync();

        var check = await app.CheckPasswordAsync("  ALICE ", InboxApp.AlicePassword);

        Assert.Equal((SignInOutcome.Succeeded, "alice"), (check.Outcome, check.Name));
    }

    [Theory]
    [InlineData("al\x0130ce")]
    [InlineData("\x212Aate")]
    [InlineData("al\x00EFce")]
    public void Names_outside_ascii_are_refused_before_lower_casing(string name) => Assert.Null(OperatorAccounts.NormalizeName(name));

    [Fact]
    public async Task A_name_with_the_kelvin_sign_does_not_reach_kate()
    {
        await using var app = new InboxApp();
        await app.CreateOperatorAsync("kate");

        // Lower-casing turns U+212A KELVIN SIGN into an ASCII k.
        Assert.Equal(new PasswordCheck(SignInOutcome.Failed, null), await app.CheckPasswordAsync("\x212Aate", InboxApp.AlicePassword));
    }

    [Theory]
    [InlineData(128, 1, true)]
    [InlineData(129, 1, false)]
    [InlineData(15, 2, true)]
    [InlineData(14, 2, false)]
    [InlineData(128, 2, true)]
    public async Task Password_length_counts_unicode_characters(int count, int utf16PerCharacter, bool accepted)
    {
        await using var app = new InboxApp();
        // U+1F510 takes two UTF-16 units and counts as one character.
        var password = string.Concat(Enumerable.Repeat(utf16PerCharacter == 1 ? "a" : "\U0001F510", count));

        var created = await app.CreateOperatorAsync("alice", password);

        Assert.Equal(accepted, created.Succeeded);
    }
}
