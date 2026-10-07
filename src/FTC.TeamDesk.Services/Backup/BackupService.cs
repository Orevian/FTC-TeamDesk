using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

/// <summary>
/// Backup = zip(manifest + consistent SQLite copy). The vault inside stays encrypted; API keys and Google tokens live
/// outside the database (DPAPI secret store) and are therefore never part of a backup or export.
/// Export/Import = portable JSON of team data; vault entries appear only as ciphertext.
/// </summary>
public sealed class BackupService : IBackupService
{
    public const string BackupExtension = ".tdbackup";
    private const string DbEntry = "teamdesk.db";
    private const string ManifestEntry = "manifest.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IDatabaseMaintenance _db;
    private readonly IActivityLogService _log;

    public BackupService(IDatabaseMaintenance db, IActivityLogService log) { _db = db; _log = log; }

    public async Task<string> BackupAsync(string destinationFile, CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"teamdesk-{Guid.NewGuid():N}.db");
        try
        {
            await _db.BackupToAsync(temp, ct).ConfigureAwait(false);
            if (File.Exists(destinationFile)) File.Delete(destinationFile);
            using (var zip = ZipFile.Open(destinationFile, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(temp, DbEntry, CompressionLevel.Optimal);
                var manifest = zip.CreateEntry(ManifestEntry);
                await using var stream = manifest.Open();
                await JsonSerializer.SerializeAsync(stream, new { app = "FTC TeamDesk", format = 1, createdUtc = DateTime.UtcNow }, Json, ct).ConfigureAwait(false);
            }
        }
        finally { TryDelete(temp); }
        await _log.LogAsync(ActivityActions.BackupCreated, Path.GetFileName(destinationFile), ActivityActor.User, ct).ConfigureAwait(false);
        return destinationFile;
    }

    public async Task RestoreAsync(string backupFile, CancellationToken ct = default)
    {
        if (!File.Exists(backupFile)) throw new DomainException("Error.FileNotFound");
        var temp = Path.Combine(Path.GetTempPath(), $"teamdesk-restore-{Guid.NewGuid():N}.db");
        try
        {
            try
            {
                using var zip = ZipFile.OpenRead(backupFile);
                var entry = zip.GetEntry(DbEntry) ?? throw new DomainException("Error.InvalidBackup");
                entry.ExtractToFile(temp, overwrite: true);
            }
            catch (InvalidDataException) { throw new DomainException("Error.InvalidBackup"); }

            await _log.LogAsync(ActivityActions.BackupRestored, Path.GetFileName(backupFile), ActivityActor.User, ct).ConfigureAwait(false);
            await _db.RestoreFromAsync(temp, ct).ConfigureAwait(false);
        }
        finally { TryDelete(temp); }
    }

    public async Task ExportAsync(string destinationFile, CancellationToken ct = default)
    {
        var snapshot = await _db.ExportSnapshotAsync(ct).ConfigureAwait(false);
        await using var stream = File.Create(destinationFile);
        await JsonSerializer.SerializeAsync(stream, snapshot, Json, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.DataExported, Path.GetFileName(destinationFile), ActivityActor.User, ct).ConfigureAwait(false);
    }

    public async Task<int> ImportAsync(string sourceFile, CancellationToken ct = default)
    {
        if (!File.Exists(sourceFile)) throw new DomainException("Error.FileNotFound");
        DataSnapshot? snapshot;
        try
        {
            await using var stream = File.OpenRead(sourceFile);
            snapshot = await JsonSerializer.DeserializeAsync<DataSnapshot>(stream, Json, ct).ConfigureAwait(false);
        }
        catch (JsonException) { throw new DomainException("Error.InvalidImportFile"); }
        if (snapshot is null) throw new DomainException("Error.InvalidImportFile");

        var added = await _db.ImportSnapshotAsync(snapshot, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.DataImported, added.ToString(System.Globalization.CultureInfo.InvariantCulture), ActivityActor.User, ct).ConfigureAwait(false);
        return added;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* temp file, best effort */ }
    }
}
