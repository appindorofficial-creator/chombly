using System.Security.Cryptography;
using System.Text;

namespace WebAppPet.Services;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("pawcare:" + password));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string password, string hash) =>
        string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
}
