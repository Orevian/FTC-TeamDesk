using System.Security.Cryptography;
using FTC.TeamDesk.Security;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class VaultCryptoTests
{
    private readonly VaultCrypto _crypto = new();

    [Fact]
    public void EncryptDecryptRoundTrips()
    {
        var key = _crypto.DeriveKey("correct horse battery", _crypto.GenerateSalt(), 100_000);
        var blob = _crypto.Encrypt(key, System.Text.Encoding.UTF8.GetBytes("s3cret!"));
        Assert.Equal("s3cret!", System.Text.Encoding.UTF8.GetString(_crypto.Decrypt(key, blob)));
    }

    [Fact]
    public void CiphertextDoesNotContainPlaintext()
    {
        var key = _crypto.DeriveKey("pw-pw-pw-pw", _crypto.GenerateSalt(), 100_000);
        var plain = System.Text.Encoding.UTF8.GetBytes("VerySecretPassword123");
        var blob = _crypto.Encrypt(key, plain);
        Assert.DoesNotContain("VerySecretPassword123", System.Text.Encoding.UTF8.GetString(blob));
    }

    [Fact]
    public void SamePlaintextGivesDifferentCiphertext()
    {
        var key = _crypto.DeriveKey("pw-pw-pw-pw", _crypto.GenerateSalt(), 100_000);
        var p = new byte[] { 1, 2, 3, 4 };
        Assert.NotEqual(Convert.ToBase64String(_crypto.Encrypt(key, p)), Convert.ToBase64String(_crypto.Encrypt(key, p)));
    }

    [Fact]
    public void WrongKeyFails()
    {
        var salt = _crypto.GenerateSalt();
        var blob = _crypto.Encrypt(_crypto.DeriveKey("right-password", salt, 100_000), new byte[] { 9, 9, 9 });
        var wrong = _crypto.DeriveKey("wrong-password", salt, 100_000);
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(wrong, blob));
    }

    [Fact]
    public void TamperedCiphertextFails()
    {
        var key = _crypto.DeriveKey("right-password", _crypto.GenerateSalt(), 100_000);
        var blob = _crypto.Encrypt(key, new byte[] { 1, 2, 3, 4, 5 });
        blob[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => _crypto.Decrypt(key, blob));
    }

    [Fact]
    public void KeyDerivationIsSaltedAndDeterministic()
    {
        var s1 = _crypto.GenerateSalt();
        var s2 = _crypto.GenerateSalt();
        Assert.NotEqual(Convert.ToBase64String(s1), Convert.ToBase64String(s2));
        Assert.Equal(Convert.ToBase64String(_crypto.DeriveKey("abc-abc-abc", s1, 100_000)), Convert.ToBase64String(_crypto.DeriveKey("abc-abc-abc", s1, 100_000)));
        Assert.NotEqual(Convert.ToBase64String(_crypto.DeriveKey("abc-abc-abc", s1, 100_000)), Convert.ToBase64String(_crypto.DeriveKey("abc-abc-abc", s2, 100_000)));
    }

    [Fact]
    public void RejectsWeakIterationCount()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _crypto.DeriveKey("x", _crypto.GenerateSalt(), 1000));
}

public class AttemptPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 20)]
    public void BackoffGrowsExponentially(int failures, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), AttemptPolicy.LockoutFor(failures));

    [Fact]
    public void BackoffIsCapped() => Assert.Equal(AttemptPolicy.MaxLockout, AttemptPolicy.LockoutFor(500));
}

public class PasswordGeneratorTests
{
    [Fact]
    public void GeneratesRequestedLengthWithAllClasses()
    {
        for (var i = 0; i < 50; i++)
        {
            var pw = PasswordGenerator.Generate(new PasswordOptions(Length: 12));
            Assert.Equal(12, pw.Length);
            Assert.True(pw.Any(char.IsLower) && pw.Any(char.IsUpper) && pw.Any(char.IsDigit) && pw.Any(c => !char.IsLetterOrDigit(c)));
        }
    }

    [Fact]
    public void AvoidsAmbiguousCharacters()
    {
        for (var i = 0; i < 50; i++)
            Assert.DoesNotContain(PasswordGenerator.Generate(new PasswordOptions(Length: 40)), c => "l0O1I".Contains(c));
    }

    [Fact]
    public void RequiresAtLeastOneClass()
        => Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(new PasswordOptions(Lower: false, Upper: false, Digits: false, Symbols: false)));

    [Fact]
    public void StrengthScoreOrdersPasswords()
    {
        Assert.True(PasswordGenerator.Estimate("abc").Score < PasswordGenerator.Estimate("Tr0ub4dor&3-horse-staple").Score);
        Assert.Equal(0, PasswordGenerator.Estimate("").Score);
    }
}

public class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("password=hunter2", "hunter2")]
    [InlineData("{\"password\":\"hunter2\"}", "hunter2")]
    [InlineData("api_key: abcdef123456", "abcdef123456")]
    [InlineData("Authorization: Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("GET /v1?key=SuperSecretValue&x=1", "SuperSecretValue")]
    [InlineData("used sk-abcdefghijklmnop1234 for call", "sk-abcdefghijklmnop1234")]
    [InlineData("token: ya29.a0AfH6SMBxxxxxxxxxxxx", "ya29.a0AfH6SMBxxxxxxxxxxxx")]
    [InlineData("refresh_token=1//0gAbCdEfGhIjKlMnOpQrStUv", "1//0gAbCdEfGhIjKlMnOpQrStUv")]
    public void RemovesSecrets(string input, string secret)
    {
        var result = SensitiveDataRedactor.Redact(input);
        Assert.DoesNotContain(secret, result);
        Assert.Contains(SensitiveDataRedactor.Mask, result);
    }

    [Fact]
    public void LeavesOrdinaryTextAlone() => Assert.Equal("Purchase added: Servo x4", SensitiveDataRedactor.Redact("Purchase added: Servo x4"));
}
