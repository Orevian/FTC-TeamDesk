namespace FTC.TeamDesk.Infrastructure;

/// <summary>All per-user file locations. Nothing is written next to the executable or into the repository.</summary>
public sealed class AppPaths
{
    public AppPaths(string? overrideRoot = null)
    {
        Root = overrideRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FTC TeamDesk");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string DefaultDatabase => Path.Combine(Root, "teamdesk.db");
    public string Preferences => Path.Combine(Root, "preferences.json");
    public string Secrets => Path.Combine(Root, "secrets");
    public string Logs => Path.Combine(Root, "logs");
}
