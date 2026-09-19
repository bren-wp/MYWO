using System.Security.Cryptography;
using System.Text;
using MYWO.Desktop.Data;

namespace MYWO.Desktop.Services;

public static class ApiKeyService
{
    public static string Create(long companyId, string name, string permissions)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "API ključ";
        var random = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(random)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var key = "mywo_live_" + token;
        var prefix = key[..Math.Min(18, key.Length)] + "…";
        AppDb.InsertApiKey(companyId, name.Trim(), prefix, Hash(key), permissions);
        return key;
    }

    public static string Hash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
