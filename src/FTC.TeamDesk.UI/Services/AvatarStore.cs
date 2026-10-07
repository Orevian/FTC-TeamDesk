using FTC.TeamDesk.UI.Abstractions;

namespace FTC.TeamDesk.UI.Services;

/// <summary>Copies chosen profile pictures into the app data folder so they survive the original being moved or deleted.</summary>
public sealed class AvatarStore : IAvatarStore
{
    private static readonly string[] Allowed = { ".png", ".jpg", ".jpeg", ".bmp" };
    private readonly string _folder;

    public AvatarStore(string dataFolder) { _folder = Path.Combine(dataFolder, "avatars"); }

    public async Task<string> ImportAsync(string sourceFile, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
        if (!Allowed.Contains(ext)) throw new Core.Common.DomainException("Error.UnsupportedImage");
        var info = new FileInfo(sourceFile);
        if (info.Length > 8 * 1024 * 1024) throw new Core.Common.DomainException("Error.ImageTooLarge");
        Directory.CreateDirectory(_folder);
        var target = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ext);
        await using var src = File.OpenRead(sourceFile);
        await using var dst = File.Create(target);
        await src.CopyToAsync(dst, ct).ConfigureAwait(false);
        return target;
    }
}
