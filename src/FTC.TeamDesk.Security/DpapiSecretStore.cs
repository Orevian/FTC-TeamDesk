using System.Security.Cryptography;
using System.Text;
using FTC.TeamDesk.Core.Abstractions.Services;

namespace FTC.TeamDesk.Security;

/// <summary>
/// Persists secrets (API keys, OAuth tokens) encrypted with Windows DPAPI, scoped to the current Windows user.
/// One file per secret under %LOCALAPPDATA%\FTC TeamDesk\secrets. File names are hashes, so they reveal nothing.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FTC.TeamDesk.SecretStore.v1");
    private readonly string _directory;

    public DpapiSecretStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public Task SetAsync(string name, string secret, CancellationToken ct = default)
    {
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
        return File.WriteAllBytesAsync(PathFor(name), protectedBytes, ct);
    }

    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        try
        {
            var data = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            // Different Windows user or corrupted file: treat as missing so the user can re-enter the secret.
            return null;
        }
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(File.Exists(PathFor(name)));

    private string PathFor(string name)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
        return Path.Combine(_directory, hash[..32] + ".bin");
    }
}
