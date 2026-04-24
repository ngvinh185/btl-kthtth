using System.Security.Cryptography;
using System.Text;

namespace BTL_WEB.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return ComputeSha256Hex(password);
    }

    public bool Verify(string inputPassword, string storedPassword)
    {
        if (string.Equals(inputPassword, storedPassword, StringComparison.Ordinal))
        {
            return true;
        }

        var sha256Hex = ComputeSha256Hex(inputPassword);
        if (string.Equals(sha256Hex, storedPassword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sha256Base64 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(inputPassword)));
        return string.Equals(sha256Base64, storedPassword, StringComparison.Ordinal);
    }

    private static string ComputeSha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(hash.Length * 2);

        foreach (var item in hash)
        {
            builder.Append(item.ToString("x2"));
        }

        return builder.ToString();
    }
}
