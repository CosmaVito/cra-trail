using CvdInbox.Reports;

namespace CvdInbox.Tests;

public class ReportInputTests
{
    [Fact]
    public void A_complete_report_is_trimmed_and_accepted()
    {
        var errors = ReportInput.Validate(Report(product: "  Example Product  ", contact: " researcher@example.test "), out var fields);

        Assert.Empty(errors);
        Assert.Equal("Example Product", fields.Product);
        Assert.Equal("researcher@example.test", fields.Contact);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void The_contact_is_optional(string? contact)
    {
        Assert.Empty(ReportInput.Validate(Report(contact: contact), out var fields));
        Assert.Null(fields.Contact);
    }

    [Theory]
    [InlineData("product", 101)]
    [InlineData("version", 51)]
    [InlineData("description", 19)]
    [InlineData("description", 20_001)]
    [InlineData("contact", 201)]
    public void Lengths_are_checked(string field, int length)
    {
        var text = new string('a', length);
        var report = field switch
        {
            "product" => Report(product: text),
            "version" => Report(version: text),
            "description" => Report(description: text),
            _ => Report(contact: text),
        };

        Assert.Equal(new[] { field }, ReportInput.Validate(report, out _).Keys);
    }

    [Theory]
    [InlineData("Widget\u202E")]
    [InlineData("Widget\u2066")]
    [InlineData("Wid\nget")]
    [InlineData("Wid\0get")]
    public void Short_fields_refuse_control_and_bidi_characters(string product) =>
        Assert.Contains("product", ReportInput.Validate(Report(product: product), out _).Keys);

    [Fact]
    public void The_description_keeps_line_breaks_and_tabs_but_not_other_controls()
    {
        Assert.Empty(ReportInput.Validate(Report(description: "Steps:\n1.\tOpen the page\r\n2. Post a comment"), out _));
        Assert.Contains("description", ReportInput.Validate(Report(description: "Steps to reproduce the bug\u0007 here"), out _).Keys);
        Assert.Contains("description", ReportInput.Validate(Report(description: "Steps to reproduce \u202E the bug here"), out _).Keys);
    }

    private static NewReport Report(
        string? product = "Example Product",
        string? version = "1.2.3",
        string? description = "Stored XSS in the comment field of the admin page.",
        string? contact = null) =>
        new(product, version, description, contact, Website: null);
}
