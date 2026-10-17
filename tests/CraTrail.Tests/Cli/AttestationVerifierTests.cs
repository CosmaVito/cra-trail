using System.ComponentModel;
using CraTrail.Cli.Attestation;

namespace CraTrail.Tests.Cli;

public class AttestationVerifierTests
{
    private const string Repository = "CosmaVito/cra-trail";
    private const string Workflow = "CosmaVito/cra-trail/.github/workflows/release.yml";
    private const string Root = "{\"mediaType\":\"application/vnd.dev.sigstore.trustedroot+json;version=0.1\"}\n";

    [Fact]
    public void Gh_checks_the_evaluation_predicate_offline_against_the_trusted_root() =>
        Assert.Equal(
            new[]
            {
                "attestation", "verify", "manifest.json", "--bundle", "b.sigstore.json", "--repo", Repository,
                "--predicate-type", "https://cosmavito.github.io/cra-trail/predicates/evaluation/v0.1",
                "--custom-trusted-root", "trusted_root.jsonl",
                "--signer-workflow", Workflow,
            },
            GhAttestationVerifier.Arguments("manifest.json", "b.sigstore.json", Repository, Workflow, "trusted_root.jsonl"));

    [Fact]
    public void A_good_bundle_is_verified_against_the_root_that_gh_fetched()
    {
        var gh = new FakeGh(new GhRun(0, Root, "", false), new GhRun(0, "", "", false));

        var result = Verify(gh);

        Assert.Equal(SignatureStatus.Verified, result.Status);
        Assert.Equal(new[] { "attestation", "trusted-root" }, gh.Calls[0]);
        Assert.Equal("verify", gh.Calls[1][1]);
        Assert.Equal(Root, gh.TrustedRoot);
    }

    [Fact]
    public void Without_the_trusted_root_the_signature_is_not_checked_rather_than_invalid()
    {
        var gh = new FakeGh(new GhRun(1, "", "failed to fetch trusted root: dial tcp: lookup tuf-repo-cdn.sigstore.dev: no such host", false));

        var result = Verify(gh);

        Assert.Equal(
            new SignatureResult(SignatureStatus.Unavailable, "could not fetch the Sigstore trusted root: failed to fetch trusted root: dial tcp: lookup tuf-repo-cdn.sigstore.dev: no such host"),
            result);
        Assert.Single(gh.Calls);
    }

    [Fact]
    public void A_bundle_that_does_not_match_is_invalid()
    {
        var gh = new FakeGh(new GhRun(0, Root, "", false), new GhRun(1, "", "Error: verifying with issuer \"sigstore.dev\": no matching attestations", false));

        Assert.Equal(new SignatureResult(SignatureStatus.Invalid, "Error: verifying with issuer \"sigstore.dev\": no matching attestations"), Verify(gh));
    }

    [Fact]
    public void A_step_that_hangs_is_reported_as_not_checked()
    {
        Assert.Equal(
            new SignatureResult(SignatureStatus.Unavailable, "gh attestation trusted-root did not finish within 60 s"),
            Verify(new FakeGh(new GhRun(-1, "", "", true))));
        Assert.Equal(
            new SignatureResult(SignatureStatus.Unavailable, "gh attestation verify did not finish within 60 s"),
            Verify(new FakeGh(new GhRun(0, Root, "", false), new GhRun(-1, "", "", true))));
    }

    [Fact]
    public void A_gh_that_wants_a_login_is_not_taken_for_a_bad_signature() =>
        Assert.Equal(
            new SignatureResult(SignatureStatus.Unavailable, "gh needs a login: run gh auth login"),
            Verify(new FakeGh(new GhRun(0, Root, "", false), new GhRun(4, "", "To get started with GitHub CLI, please run: gh auth login", false))));

    [Fact]
    public void A_missing_gh_is_reported_as_not_checked() =>
        Assert.Equal(new SignatureResult(SignatureStatus.Unavailable, "gh CLI not found"), Verify(new FakeGh { Missing = true }));

    private static SignatureResult Verify(FakeGh gh) =>
        new GhAttestationVerifier(gh).Verify("{}"u8.ToArray(), "b.sigstore.json", Repository, Workflow);

    private sealed class FakeGh(params GhRun[] runs) : IGhRunner
    {
        private readonly Queue<GhRun> answers = new(runs);

        public bool Missing { get; init; }

        public List<IReadOnlyList<string>> Calls { get; } = [];

        // The file that verify got, read while it still exists.
        public string? TrustedRoot { get; private set; }

        public GhRun Run(IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            if (Missing)
                throw new Win32Exception(2, "No such file or directory");
            Calls.Add(arguments);
            var root = arguments.ToList().IndexOf("--custom-trusted-root");
            if (root >= 0)
                TrustedRoot = File.ReadAllText(arguments[root + 1]);
            return answers.Dequeue();
        }
    }
}
