using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data.Repositories;

public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public ApplicationRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<PagedResult<TeamApplication>> QueryAsync(ApplicationQuery query, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        IQueryable<TeamApplication> q = db.Applications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var p = $"%{query.Search.Trim()}%";
            q = q.Where(a => EF.Functions.Like(a.ApplicantName, p)
                             || (a.Email != null && EF.Functions.Like(a.Email, p))
                             || (a.Skills != null && EF.Functions.Like(a.Skills, p))
                             || (a.Experience != null && EF.Functions.Like(a.Experience, p))
                             || a.Answers.Any(x => EF.Functions.Like(x.Answer, p)));
        }
        if (query.Status is { } st) q = q.Where(a => a.Status == st);
        if (query.From is { } from) q = q.Where(a => a.SubmittedAt >= from.Date);
        if (query.To is { } to) q = q.Where(a => a.SubmittedAt < to.Date.AddDays(1));

        var total = await q.CountAsync(ct).ConfigureAwait(false);

        var desc = query.Direction == SortDirection.Descending;
        q = query.SortBy switch
        {
            ApplicationSortField.ApplicantName => desc ? q.OrderByDescending(a => a.ApplicantName) : q.OrderBy(a => a.ApplicantName),
            ApplicationSortField.Status => desc ? q.OrderByDescending(a => a.Status) : q.OrderBy(a => a.Status),
            _ => desc ? q.OrderByDescending(a => a.SubmittedAt) : q.OrderBy(a => a.SubmittedAt)
        };

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 500);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<TeamApplication>(items, total, page, size);
    }

    public async Task<IReadOnlyList<TeamApplication>> ListAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Applications.AsNoTracking().Include(a => a.Answers.OrderBy(x => x.SortOrder))
            .OrderByDescending(a => a.SubmittedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<TeamApplication?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Applications.AsNoTracking().Include(a => a.Answers.OrderBy(x => x.SortOrder))
            .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TeamApplication>> RecentAsync(int count, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Applications.AsNoTracking().OrderByDescending(a => a.SubmittedAt).Take(count).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Applications.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false)
                  ?? throw new DomainException("Error.NotFound");
        app.Status = status;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateNotesAsync(Guid id, string? notes, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false)
                  ?? throw new DomainException("Error.NotFound");
        app.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> AddImportedAsync(IReadOnlyList<TeamApplication> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return 0;
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var keys = items.Select(i => i.ExternalKey).Where(k => k != null).Cast<string>().ToList();
        var known = (await db.Applications.Where(a => a.ExternalKey != null && keys.Contains(a.ExternalKey))
            .Select(a => a.ExternalKey!).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();

        var added = 0;
        foreach (var item in items)
        {
            if (item.ExternalKey != null && known.Contains(item.ExternalKey)) continue;
            db.Applications.Add(item);
            if (item.ExternalKey != null) known.Add(item.ExternalKey);
            added++;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return added;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Applications.Where(a => a.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
