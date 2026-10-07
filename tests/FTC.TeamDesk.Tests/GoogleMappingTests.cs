using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Services.Google;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class GoogleMappingTests
{
    private static IReadOnlyList<IReadOnlyList<string>> Rows(params string[][] rows) => rows.Select(r => (IReadOnlyList<string>)r).ToList();

    [Fact]
    public void MapsEnglishFormColumns()
    {
        var rows = Rows(
            new[] { "Timestamp", "Full name", "Email Address", "Which skills do you have?", "Previous experience", "Why join?" },
            new[] { "9/28/2026 14:03:11", "Ada Sahin", "ada@example.com", "Java, CAD", "2 seasons", "Love robots" });
        var app = ApplicationRowMapper.Map("sheet", "Form Responses 1", rows).Single();
        Assert.Equal("Ada Sahin", app.ApplicantName);
        Assert.Equal("ada@example.com", app.Email);
        Assert.Equal("Java, CAD", app.Skills);
        Assert.Equal("2 seasons", app.Experience);
        Assert.Equal(ApplicationStatus.Pending, app.Status);
        Assert.Equal(new DateTime(2026, 9, 28, 14, 3, 11), app.SubmittedAt);
    }

    [Fact]
    public void MapsTurkishFormColumns()
    {
        var rows = Rows(
            new[] { "Zaman damgası", "Adınız Soyadınız", "E-posta", "Yetenekleriniz", "Deneyiminiz" },
            new[] { "28.09.2026 14:03:11", "Elif Yılmaz", "elif@example.com", "CAD", "1 yıl" });
        var app = ApplicationRowMapper.Map("sheet", "Yanıtlar", rows).Single();
        Assert.Equal("Elif Yılmaz", app.ApplicantName);
        Assert.Equal("CAD", app.Skills);
        Assert.Equal("1 yıl", app.Experience);
        Assert.Equal(new DateTime(2026, 9, 28, 14, 3, 11), app.SubmittedAt);
    }

    [Fact]
    public void KeepsEveryNonEmptyAnswerReadable()
    {
        var rows = Rows(new[] { "Timestamp", "Name", "Q1", "Q2" }, new[] { "1/1/2026", "Bob", "answer one", "" });
        var app = ApplicationRowMapper.Map("s", "t", rows).Single();
        Assert.Contains(app.Answers, a => a.Question == "Q1" && a.Answer == "answer one");
        Assert.DoesNotContain(app.Answers, a => a.Question == "Q2");
    }

    [Fact]
    public void ExternalKeyIsStableAcrossSyncs()
    {
        var rows = Rows(new[] { "Timestamp", "Name", "Email" }, new[] { "1/1/2026", "Bob", "b@x.com" });
        Assert.Equal(ApplicationRowMapper.Map("s", "t", rows).Single().ExternalKey, ApplicationRowMapper.Map("s", "t", rows).Single().ExternalKey);
        Assert.NotEqual(ApplicationRowMapper.Map("s", "t", rows).Single().ExternalKey, ApplicationRowMapper.Map("other", "t", rows).Single().ExternalKey);
    }

    [Fact]
    public void SkipsBlankRowsAndHandlesHeaderOnly()
    {
        Assert.Empty(ApplicationRowMapper.Map("s", "t", Rows(new[] { "Name" })));
        Assert.Empty(ApplicationRowMapper.Map("s", "t", Rows(new[] { "Name" }, new[] { "", "" })));
    }

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789/edit#gid=0", "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789")]
    [InlineData("1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789", "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456789")]
    public void ExtractsSpreadsheetId(string input, string expected) => Assert.Equal(expected, GoogleSheetsClient.ExtractSpreadsheetId(input));

    [Fact]
    public void RejectsGarbageSpreadsheetInput()
        => Assert.Throws<GoogleApiException>(() => GoogleSheetsClient.ExtractSpreadsheetId("not a sheet"));
}
