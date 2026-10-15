using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation.Checks;

public class AttestedFieldCheckTests
{
    private static readonly CheckDefinition SupportEnd = TestChecks.Define("support-end-set", "type: attested-field\nfield: product.supportEnd");
    private static readonly CheckDefinition Signing = TestChecks.Define("distribution-signed", "type: attested-field\nfield: manifest.signing.method");

    [Fact]
    public void Passes_when_the_product_field_is_set() =>
        Assert.Equal(Statuses.Pass, Evaluate(SupportEnd, new TestPackBuilder()).Status);

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Is_missing_when_the_product_field_is_empty(string? value)
    {
        var outcome = Evaluate(SupportEnd, new TestPackBuilder().WithProduct(TestProducts.Default with { SupportEnd = value }));
        Assert.Equal(Statuses.Missing, outcome.Status);
        Assert.Equal(new[] { "product.supportEnd is not set" }, outcome.Details);
    }

    [Fact]
    public void Reads_fields_from_the_manifest()
    {
        Assert.Equal(Statuses.Pass, Evaluate(Signing, new TestPackBuilder().WithSigning("github-artifact-attestations")).Status);
        var unsigned = Evaluate(Signing, new TestPackBuilder());
        Assert.Equal(Statuses.Missing, unsigned.Status);
        Assert.Equal(new[] { "manifest.signing.method is not set" }, unsigned.Details);
    }

    private static CheckOutcome Evaluate(CheckDefinition check, TestPackBuilder pack) =>
        new AttestedFieldCheck().Evaluate(check, pack.Context());
}
