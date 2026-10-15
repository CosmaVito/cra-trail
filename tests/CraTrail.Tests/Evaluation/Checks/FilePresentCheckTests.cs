using CraTrail.Core.Evaluation;
using CraTrail.Core.Evaluation.Checks;
using CraTrail.Core.Pack;
using CraTrail.Core.Rules;

namespace CraTrail.Tests.Evaluation.Checks;

public class FilePresentCheckTests
{
    private static readonly CheckDefinition Check = TestChecks.Define("sbom-present", "type: file-present\nkind: product-sbom");

    [Fact]
    public void Passes_when_a_file_of_the_kind_is_in_the_pack()
    {
        var outcome = new FilePresentCheck().Evaluate(Check, new TestPackBuilder().Add("sbom/product.cdx.json", EvidenceKinds.ProductSbom, "{}").Context());
        Assert.Equal("sbom-present", outcome.Id);
        Assert.Equal("file-present", outcome.Type);
        Assert.Equal(Statuses.Pass, outcome.Status);
        Assert.Empty(outcome.Details);
    }

    [Fact]
    public void Is_missing_when_no_file_of_the_kind_is_in_the_pack()
    {
        var outcome = new FilePresentCheck().Evaluate(Check, new TestPackBuilder().Context());
        Assert.Equal(Statuses.Missing, outcome.Status);
        Assert.Equal(new[] { "no file of kind product-sbom" }, outcome.Details);
    }
}
