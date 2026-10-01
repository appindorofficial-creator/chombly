using System.Security.Cryptography;
using WebAppPet.Infrastructure.Security;

namespace WebAppPet.Tests.Infrastructure;

public class PasswordHasherTests
{
    // SHA-256 of "pawcare:123456": what the app saved before v2 and what Scripts/alta-negocios*.sql still insert.
    private const string LegacyDemoHash = "71B6822175808E2BCCD340218EF0EB319FC11A62368447891D0744862737F4E6";

    [Fact]
    public void New_hashes_use_pbkdf2_with_a_different_salt_each_time()
    {
        var first = PasswordHasher.Hash("Clave#2026");
        var second = PasswordHasher.Hash("Clave#2026");

        Assert.StartsWith("v2$600000$", first);
        Assert.NotEqual(first, second);
        Assert.True(first.Length <= 200, "AppUser.PasswordHash column holds 200 characters");
        Assert.Equal(PasswordCheck.Success, PasswordHasher.Check("Clave#2026", first));
        Assert.Equal(PasswordCheck.Success, PasswordHasher.Check("Clave#2026", second));
        Assert.Equal(PasswordCheck.Failed, PasswordHasher.Check("clave#2026", first));
    }

    [Theory]
    [InlineData(LegacyDemoHash)]
    [InlineData("71b6822175808e2bccd340218ef0eb319fc11a62368447891d0744862737f4e6")]
    public void Old_sha256_hashes_still_sign_in_and_ask_to_be_upgraded(string stored)
    {
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHasher.Check("123456", stored));
        Assert.Equal(PasswordCheck.Failed, PasswordHasher.Check("1234567", stored));
    }

    [Fact]
    public void Fewer_iterations_than_today_ask_to_be_upgraded()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2("Clave#2026", salt, 1_000, HashAlgorithmName.SHA256, 32);
        var stored = $"v2$1000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHasher.Check("Clave#2026", stored));
        Assert.Equal(PasswordCheck.Failed, PasswordHasher.Check("Otra#2026", stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-hash")]
    [InlineData("v2$abc$c2FsdA==$aGFzaA==")]
    [InlineData("v2$0$c2FsdA==$aGFzaA==")]
    [InlineData("v2$1000$%%%$aGFzaA==")]
    [InlineData("v3$1000$c2FsdA==$aGFzaA==")]
    public void Damaged_or_unknown_hashes_never_sign_in(string stored)
    {
        Assert.Equal(PasswordCheck.Failed, PasswordHasher.Check("123456", stored));
    }
}
