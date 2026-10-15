namespace CraTrail.Core.Pack;

public static class EvidenceKinds
{
    public const string Product = "product";
    public const string Evaluation = "evaluation";
    public const string ProductSbom = "product-sbom";
    public const string Sbom = "sbom";
    public const string Vex = "vex";
    public const string Grype = "grype";
    public const string SastSarif = "sast-sarif";
    public const string SecretsSarif = "secrets-sarif";
    public const string CvdPolicy = "cvd-policy";
    public const string AttestedDocument = "attested-document";
    public const string TestResults = "test-results";
    public const string SbomQuality = "sbom-quality";
    public const string NuGetAudit = "nuget-audit";
    public const string NpmAuditSignatures = "npm-audit-signatures";

    public static IReadOnlyList<string> ExtraEvidence { get; } = [TestResults, SbomQuality, NuGetAudit, NpmAuditSignatures];
}
