using System.Security.Cryptography;
using System.Text;

namespace WebAppPet.Infrastructure.Security;

public enum PasswordCheck
{
    Failed,
    Success,
    /// <summary>The password is right but was stored in an older format or with fewer iterations.</summary>
    SuccessRehashNeeded
}

/// <summary>
/// PBKDF2-HMAC-SHA256 with a random salt per user, stored as "v2$iterations$salt$hash" (Base64).
/// Hashes saved before v2 (unsalted SHA-256 in hex, also inserted by the Scripts/alta-negocios*.sql files)
/// are still accepted so the user can sign in and be upgraded.
/// </summary>
public static class PasswordHasher
{
    private const string Version = "v2";
    // OWASP's recommendation for PBKDF2-HMAC-SHA256. It travels inside each hash, so it can be raised
    // later and users are upgraded at their next sign-in.
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string LegacyPrefix = "pawcare:";

    private static readonly Lazy<string> DummyHash = new(() => Hash(Guid.NewGuid().ToString()));

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Version}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored) => Check(password, stored) != PasswordCheck.Failed;

    public static PasswordCheck Check(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return PasswordCheck.Failed;

        var parts = stored.Split('$');
        if (parts.Length == 1)
            return LegacyMatches(password, stored) ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Failed;

        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return PasswordCheck.Failed;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordCheck.Failed;
        return iterations < Iterations ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Success;
    }

    /// <summary>
    /// Takes about as long as a real check, so a sign-in with an unknown email cannot be told apart
    /// from a wrong password by its response time.
    /// </summary>
    public static void SimulateCheck(string password) => Check(password, DummyHash.Value);

    private static bool LegacyMatches(string password, string stored)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(stored);
        }
        catch (FormatException)
        {
            return false;
        }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(LegacyPrefix + password));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
