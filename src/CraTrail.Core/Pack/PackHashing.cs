using System.Security.Cryptography;

namespace CraTrail.Core.Pack;

public static class PackHashing
{
    public static string Sha256Hex(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
