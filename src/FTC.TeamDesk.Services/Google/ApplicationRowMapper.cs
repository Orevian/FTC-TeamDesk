using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services.Google;

/// <summary>Converts Google Forms response rows (header row + data rows) to applications. Pure and unit-tested.</summary>
public static class ApplicationRowMapper
{
    private static readonly string[] TimestampWords = { "timestamp", "zaman damgası", "zaman damgasi", "submitted", "gönderim", "tarih" };
    private static readonly string[] NameWords = { "full name", "name", "ad soyad", "adı soyadı", "adiniz", "adınız", "isim", "ad ", "adı" };
    private static readonly string[] EmailWords = { "email", "e-mail", "e-posta", "eposta", "mail" };
    private static readonly string[] SkillWords = { "skill", "yetenek", "beceri", "abilities", "expertise" };
    private static readonly string[] ExperienceWords = { "experience", "deneyim", "tecrübe", "tecrube", "background" };

    public static ColumnMapping DetectColumns(IReadOnlyList<string> headers, ColumnMapping? manual = null)
    {
        int Find(string[] words, params int[] exclude)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (exclude.Contains(i)) continue;
                var h = headers[i].Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")) + " ";
                if (words.Any(w => h.Contains(w))) return i;
            }
            return -1;
        }
        var ts = Find(TimestampWords);
        var email = Find(EmailWords, ts);
        var name = Find(NameWords, ts, email);
        var skills = Find(SkillWords, ts, email, name);
        var exp = Find(ExperienceWords, ts, email, name, skills);
        if (name < 0) name = Enumerable.Range(0, headers.Count).FirstOrDefault(i => i != ts && i != email, -1);

        return new ColumnMapping
        {
            Timestamp = Pick(manual?.Timestamp, ts), Name = Pick(manual?.Name, name), Email = Pick(manual?.Email, email),
            Skills = Pick(manual?.Skills, skills), Experience = Pick(manual?.Experience, exp)
        };
    }

    private static int Pick(int? manual, int detected) => manual is >= 0 ? manual.Value : detected;

    public static IReadOnlyList<TeamApplication> Map(string spreadsheetId, string tab, IReadOnlyList<IReadOnlyList<string>> rows, ColumnMapping? manual = null)
    {
        if (rows.Count < 2) return Array.Empty<TeamApplication>();
        var headers = rows[0];
        var map = DetectColumns(headers, manual);
        var result = new List<TeamApplication>();

        foreach (var row in rows.Skip(1))
        {
            string Cell(int i) => i >= 0 && i < row.Count ? row[i].Trim() : string.Empty;
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var name = Cell(map.Name);
            var email = Cell(map.Email);
            var rawTs = Cell(map.Timestamp);
            if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(email) ? "-" : email;

            var app = new TeamApplication
            {
                ApplicantName = name,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                SubmittedAt = ParseTimestamp(rawTs),
                Status = ApplicationStatus.Pending,
                Skills = NullIfEmpty(Cell(map.Skills)),
                Experience = NullIfEmpty(Cell(map.Experience)),
                ExternalKey = Key(spreadsheetId, tab, rawTs, name, email)
            };
            // Every form answer stays readable in the detail view, including the ones mapped to fields above.
            for (var i = 0; i < headers.Count; i++)
            {
                var answer = Cell(i);
                if (answer.Length == 0) continue;
                app.Answers.Add(new ApplicationAnswer { ApplicationId = app.Id, Question = headers[i].Trim(), Answer = answer, SortOrder = i });
            }
            result.Add(app);
        }
        return result;
    }

    public static DateTime ParseTimestamp(string raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
            foreach (var culture in new[] { "tr-TR", "en-US", "en-GB", "" })
                if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo(culture), DateTimeStyles.AllowWhiteSpaces, out var dt)) return dt;
        return DateTime.Now;
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static string Key(string sheet, string tab, string ts, string name, string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sheet}|{tab}|{ts}|{name}|{email}"));
        return Convert.ToHexString(bytes)[..32];
    }
}
