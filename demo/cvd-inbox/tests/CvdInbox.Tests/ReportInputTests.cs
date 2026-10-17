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
    public void Lengths_are_checked(string field, int length) =>
        Assert.Equal(new[] { field }, ReportInput.Validate(ReportWith(field, new string('a', length)), out _).Keys);

    [Theory]
    [InlineData("product", ReportInput.ProductMax)]
    [InlineData("version", ReportInput.VersionMax)]
    [InlineData("description", ReportInput.DescriptionMin)]
    [InlineData("description", ReportInput.DescriptionMax)]
    [InlineData("contact", ReportInput.ContactMax)]
    public void Lengths_at_the_limits_are_accepted(string field, int length) =>
        Assert.Empty(ReportInput.Validate(ReportWith(field, new string('a', length)), out _));

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

    // Tag characters, zero-width and directional marks, the byte order mark, line and paragraph separators.
    [Theory]
    [InlineData("\U000E0041")]
    [InlineData("\u200B")]
    [InlineData("\u200E")]
    [InlineData("\u2060")]
    [InlineData("\uFEFF")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void Every_field_refuses_invisible_formatting_characters(string hidden)
    {
        Assert.Contains("product", ReportInput.Validate(Report(product: $"Wid{hidden}get"), out _).Keys);
        Assert.Contains("contact", ReportInput.Validate(Report(contact: $"researcher{hidden}@example.test"), out _).Keys);
        Assert.Contains("description", ReportInput.Validate(Report(description: $"Steps to reproduce{hidden} the bug here"), out _).Keys);
    }

    // Soft hyphen, stacked or leading variation selectors, the supplementary variation selectors, a Hangul filler.
    [Theory]
    [InlineData("Wid\x00ADget", "U+00AD")]
    [InlineData("Widget \x2764\xFE0F\xFE0F", "U+FE0F")]
    [InlineData("\xFE0F Widget", "U+FE0F")]
    [InlineData("Widget \U000E0100", "U+E0100")]
    [InlineData("Wid\x3164get", "U+3164")]
    public void The_message_names_the_hidden_character(string product, string code) =>
        Assert.Equal(new[] { $"Remove the hidden character {code}." }, ReportInput.Validate(Report(product: product), out _)["product"]);

    [Fact]
    public void A_variation_selector_after_a_character_is_accepted() =>
        Assert.Empty(ReportInput.Validate(Report(product: "Widget \x2764\xFE0F"), out _));

    // Emoji sequences and scripts such as Persian need them.
    [Theory]
    [InlineData("\u200C")]
    [InlineData("\u200D")]
    public void Zero_width_joiners_are_allowed(string joiner) =>
        Assert.Empty(ReportInput.Validate(Report(product: $"Widget \U0001F469{joiner}\U0001F4BB"), out _));

    private static NewReport ReportWith(string field, string text) => field switch
    {
        "product" => Report(product: text),
        "version" => Report(version: text),
        "description" => Report(description: text),
        _ => Report(contact: text),
    };

    private static NewReport Report(
        string? product = "Example Product",
        string? version = "1.2.3",
        string? description = "Stored XSS in the comment field of the admin page.",
        string? contact = null) =>
        new(product, version, description, contact, Website: null);
}
