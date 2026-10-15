using CvdInbox.Operators;
using Microsoft.EntityFrameworkCore;

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
}
