namespace CraTrail.Tests.Cli;

internal static class CliPacks
{
    public const string Config = """
        product:
          name: CVD Inbox
          purl: pkg:github/CosmaVito/cra-trail#demo/cvd-inbox
          support-end: 2031-12-31
          cvd-contact: security@example.org
          security-txt: https://example.org/.well-known/security.txt
        attested:
          t/risk: docs/risk-assessment.md
        not-applicable:
          t/other: Runs as a single local service with no network functions that affect other devices.
        """;

    public static void WriteRepo(CliRun run, string config = Config)
    {
        File.WriteAllText(run.Path("cra.yml"), config);
        Directory.CreateDirectory(run.Path("docs"));
        File.Copy(TestPaths.Conformance("shared", "risk-assessment.md"), run.Path("docs", "risk-assessment.md"), overwrite: true);
    }

    public static int Pack(CliRun run, string grype = "grype-clean.json", string? secrets = null, params string[] extra)
    {
        string[] args =
        [
            "pack",
            "--product-sbom", TestPaths.Conformance("shared", "sbom-clean.cdx.json"),
            "--grype", TestPaths.Conformance("shared", grype),
            "--secrets-sarif", secrets ?? TestPaths.Conformance("shared", "secrets-clean.sarif"),
            "--sast-sarif", TestPaths.Conformance("shared", "sast-clean.sarif"),
            "--rules", TestPaths.Conformance("rules"),
            "--signing-method", "github-artifact-attestations",
            "--signer-workflow", "CosmaVito/cra-trail/.github/workflows/release.yml",
            "--created-at", "2026-10-15T00:00:00Z",
            "--out", "out/pack.zip",
            .. extra,
        ];
        return run.Run(args);
    }
}
