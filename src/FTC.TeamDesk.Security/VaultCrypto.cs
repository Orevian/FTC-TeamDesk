using System.Security.Cryptography;

namespace FTC.TeamDesk.Security;

/// <summary>Cryptographic primitives used by the Password Vault.</summary>
public interface IVaultCrypto
{
    int DefaultIterations { get; }
    byte[] GenerateSalt();
    byte[] DeriveKey(string masterPassword, byte[] salt, int iterations);
    /// <summary>Returns nonce(12) || tag(16) || ciphertext.</summary>
    byte[] Encrypt(byte[] key, byte[] plaintext);
    /// <exception cref="CryptographicException">Wrong key or tampered data.</exception>
    byte[] Decrypt(byte[] key, byte[] blob);
}

/// <summary>
/// Key derivation: PBKDF2-HMAC-SHA512 with a random 32-byte salt and a high iteration count.
/// Encryption: AES-256-GCM with a fresh random 96-bit nonce per message (authenticated encryption).
/// </summary>
public sealed class VaultCrypto : IVaultCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int SaltSize = 32;

    public int DefaultIterations => 600_000;

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public byte[] DeriveKey(string masterPassword, byte[] salt, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterPassword);
        ArgumentNullException.ThrowIfNull(salt);
        if (iterations < 100_000) throw new ArgumentOutOfRangeException(nameof(iterations), "Iteration count too low.");
        return Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, iterations, HashAlgorithmName.SHA512, KeySize);
    }

    public byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        var result = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
        return result;
    }

    public byte[] Decrypt(byte[] key, byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize) throw new CryptographicException("Ciphertext is too short.");
        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var cipher = blob.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }
}
