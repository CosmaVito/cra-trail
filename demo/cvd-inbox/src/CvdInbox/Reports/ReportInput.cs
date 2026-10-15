using System.Globalization;
using System.Text;

namespace CvdInbox.Reports;

public sealed record NewReport(string? Product, string? Version, string? Description, string? Contact, string? Website);

public sealed record ReportFields(string Product, string Version, string Description, string? Contact);

public static class ReportInput
{
    public const int ProductMax = 100;
    public const int VersionMax = 50;
    public const int DescriptionMin = 20;
    public const int DescriptionMax = 20_000;
    public const int ContactMax = 200;
    public const int NoteMax = 4_000;

    public static Dictionary<string, string[]> Validate(NewReport input, out ReportFields fields)
    {
        var errors = new Dictionary<string, string[]>();
        var product = Field(input.Product, "product", 1, ProductMax, multiline: false, errors);
        var version = Field(input.Version, "version", 1, VersionMax, multiline: false, errors);
        var description = Field(input.Description, "description", DescriptionMin, DescriptionMax, multiline: true, errors);
        var contact = string.IsNullOrWhiteSpace(input.Contact)
            ? null
            : Field(input.Contact, "contact", 1, ContactMax, multiline: false, errors);
        fields = new ReportFields(product, version, description, contact);
        return errors;
    }

    // Returns the trimmed text and records in errors why it cannot be accepted.
    public static string Field(string? value, string name, int min, int max, bool multiline, Dictionary<string, string[]> errors)
    {
        var text = value?.Trim() ?? "";
        var problem = text.Length == 0 ? "This field is required."
            : text.Length < min ? $"Use at least {min} characters."
            : text.Length > max ? $"Use at most {max} characters."
            : HasForbiddenCharacters(text, multiline) ? "Remove control characters and invisible formatting characters."
            : null;
        if (problem is not null)
            errors[name] = [problem];
        return text;
    }

    // Control characters, bidi overrides and other invisible characters (zero-width spaces, tag characters, Unicode line
    // and paragraph separators) can hide or reorder text on the operator's screen (Trojan Source). Runes, not chars,
    // so characters outside the Basic Multilingual Plane, such as the tag characters, are checked too.
    public static bool HasForbiddenCharacters(string text, bool multiline)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var forbidden = Rune.GetUnicodeCategory(rune) switch
            {
                UnicodeCategory.Control => !(multiline && rune.Value is '\n' or '\r' or '\t'),
                // Zero-width non-joiner and joiner stay: emoji sequences and scripts such as Persian need them.
                UnicodeCategory.Format => rune.Value is not (0x200C or 0x200D),
                UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => true,
                _ => false,
            };
            if (forbidden)
                return true;
        }
        return false;
    }
}
