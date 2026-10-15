using CvdInbox.Reports;

namespace CvdInbox.Tests;

public class ReceiptCodeTests
{
    [Fact]
    public void Codes_are_128_random_bits_in_eight_groups()
    {
        var first = ReceiptCode.Create();

        Assert.Matches("^[0-9a-f]{4}(-[0-9a-f]{4}){7}$", first);
        Assert.NotEqual(first, ReceiptCode.Create());
    }

    [Theory]
    [InlineData("0123-4567-89ab-cdef-0123-4567-89ab-cdef")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData(" 0123 4567 89ab cdef 0123 4567 89ab cdef ")]
    public void Normalize_accepts_the_code_as_people_type_it(string typed) =>
        Assert.Equal("0123456789abcdef0123456789abcdef", ReceiptCode.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData("0123456789abcdef0123456789abcdef00")]
    public void Normalize_rejects_anything_else(string? typed) =>
        Assert.Null(ReceiptCode.Normalize(typed));

    [Fact]
    public void Hash_is_the_hex_sha256_of_the_normalized_code() =>
        Assert.Equal(
            "3eb1bd439947eb762998e566ccc2e099c791118b2f40579cc4f7da2b5061b7f9",
            ReceiptCode.Hash("0123456789abcdef0123456789abcdef"));
}
