using CraTrail.Cli.Attestation;

namespace CraTrail.Tests.Cli;

public class AttestationVerifierTests
{
    [Fact]
    public void Gh_checks_the_evaluation_predicate_of_the_manifest() =>
        Assert.Equal(
            new[]
            {
                "attestation", "verify", "manifest.json", "--bundle", "b.sigstore.json", "--repo", "CosmaVito/cra-trail",
                "--predicate-type", "https://cosmavito.github.io/cra-trail/predicates/evaluation/v0.1",
                "--signer-workflow", "CosmaVito/cra-trail/.github/workflows/release.yml",
            },
            GhAttestationVerifier.Arguments("manifest.json", "b.sigstore.json", "CosmaVito/cra-trail", "CosmaVito/cra-trail/.github/workflows/release.yml"));
}
