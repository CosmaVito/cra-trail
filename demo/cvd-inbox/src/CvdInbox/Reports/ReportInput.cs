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
            : FindHidden(text, multiline) is { } hidden ? $"Remove the hidden character U+{hidden.Value:X4}."
            : null;
        if (problem is not null)
            errors[name] = [problem];
        return text;
    }

    // The first character that can hide or reorder text on the operator's screen (Trojan Source), or null: control
    // characters, bidi overrides and other invisible characters (zero-width spaces, tag characters, Unicode line and
    // paragraph separators). Runes, not chars, so characters outside the Basic Multilingual Plane are checked too.
    public static Rune? FindHidden(string text, bool multiline)
    {
        Rune? previous = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var hidden = rune.Value switch
            {
                // A variation selector picks the text or emoji form of the character before it. Leading or stacked, it only hides.
                >= 0xFE00 and <= 0xFE0F => previous is not { } before || IsVariationSelector(before),
                >= 0xE0100 and <= 0xE01EF => true,
                // Hangul fillers are letters that draw nothing.
                0x115F or 0x1160 or 0x3164 or 0xFFA0 => true,
                _ => Rune.GetUnicodeCategory(rune) switch
                {
                    UnicodeCategory.Control => !(multiline && rune.Value is '\n' or '\r' or '\t'),
                    // Zero-width non-joiner and joiner stay: emoji sequences and scripts such as Persian need them.
                    UnicodeCategory.Format => rune.Value is not (0x200C or 0x200D),
                    UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => true,
                    _ => false,
                },
            };
            if (hidden)
                return rune;
            previous = rune;
        }
        return null;
    }

    private static bool IsVariationSelector(Rune rune) => rune.Value is >= 0xFE00 and <= 0xFE0F;
}
