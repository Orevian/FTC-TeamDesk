using System.Reflection;
using Microsoft.Data.Sqlite;

namespace FTC.TeamDesk.Data;

/// <summary>
/// Minimal forward-only migration runner. Scripts live in /Migrations as NNNN_name.sql (embedded resources),
/// run in order inside a transaction and are recorded in the SchemaVersion table.
/// </summary>
public static class SchemaMigrator
{
    public static async Task MigrateAsync(string connectionString, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await ExecuteAsync(connection, null, "PRAGMA journal_mode=WAL;", ct).ConfigureAwait(false);
        await ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL);", ct)
            .ConfigureAwait(false);

        var current = await GetCurrentVersionAsync(connection, ct).ConfigureAwait(false);
        foreach (var script in LoadScripts().Where(s => s.Version > current).OrderBy(s => s.Version))
        {
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await ExecuteAsync(connection, tx, script.Sql, ct).ConfigureAwait(false);
                await using var record = connection.CreateCommand();
                record.Transaction = tx;
                record.CommandText = "INSERT INTO SchemaVersion (Version, Name, AppliedAt) VALUES ($v, $n, $t);";
                record.Parameters.AddWithValue("$v", script.Version);
                record.Parameters.AddWithValue("$n", script.Name);
                record.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
                await record.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
    }

    public static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    public static IReadOnlyList<(int Version, string Name, string Sql)> LoadScripts()
    {
        var asm = typeof(SchemaMigrator).Assembly;
        var list = new List<(int, string, string)>();
        foreach (var resource in asm.GetManifestResourceNames().Where(n => n.StartsWith("Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal)))
        {
            var file = resource["Migrations.".Length..];
            var underscore = file.IndexOf('_');
            if (underscore <= 0 || !int.TryParse(file[..underscore], out var version)) continue;
            using var stream = asm.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            list.Add((version, Path.GetFileNameWithoutExtension(file), reader.ReadToEnd()));
        }
        return list;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
