using System.Text.Json.Nodes;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Calculations;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.AI.Tools;

/// <summary>
/// Write tools NEVER change data. They validate the request, compute the effect, and return a <see cref="PendingAction"/>
/// that the UI shows in a confirmation dialog. Only after the user confirms is the action applied (as actor "Ai").
/// </summary>
internal abstract class WriteTool : IAiTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonObject Schema { get; }
    public abstract AiPermissionKey Permission { get; }
    public bool IsWrite => true;
    public abstract Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct);

    protected static AiToolOutcome Pending(PendingAction action) => new(
        AiJson.Serialize(new { status = "pending_user_confirmation", message = "The change was NOT applied yet. The user must confirm it in a dialog. Tell the user it is waiting for their confirmation." }),
        action);

    protected static async Task<(BudgetCategory? Category, AiToolOutcome? Error)> FindCategoryAsync(IBudgetService budget, string? query, CancellationToken ct)
    {
        var cats = await budget.ListCategoriesAsync(ct).ConfigureAwait(false);
        if (query is null) return (null, AiJson.Error("missing_argument", "Provide a category.", cats.Select(c => c.Name ?? c.Key)));
        var cat = cats.FirstOrDefault(c => GetMembersTool.Matches(c.Key, c.Name, query));
        return cat is null ? (null, AiJson.Error("unknown_category", "No such category.", cats.Select(c => c.Name ?? c.Key))) : (cat, null);
    }

    protected static async Task<(Purchase? Purchase, AiToolOutcome? Error)> FindPurchaseAsync(IBudgetService budget, JsonObject args, CancellationToken ct)
    {
        var all = await budget.ListPurchasesAsync(ct).ConfigureAwait(false);
        if (args.Guid("id") is { } id)
        {
            var p = all.FirstOrDefault(x => x.Id == id);
            return p is null ? (null, AiJson.Error("not_found", "No purchase with that id.")) : (p, null);
        }
        var name = args.Str("product");
        if (name is null) return (null, AiJson.Error("missing_argument", "Provide id or product."));
        var matches = all.Where(x => x.Product.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) matches = all.Where(x => x.Product.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            0 => (null, AiJson.Error("not_found", "No purchase with that name.")),
            1 => (matches[0], null),
            _ => (null, AiJson.Error("ambiguous", "Several purchases match; use the id.", matches.Take(10).Select(x => new { id = x.Id, product = x.Product, x.Quantity, status = x.Status })))
        };
    }
}

internal sealed class AddPurchaseTool : WriteTool
{
    private readonly IBudgetService _budget;
    public AddPurchaseTool(IBudgetService budget) { _budget = budget; }
    public override string Name => "add_purchase";
    public override string Description => "Propose adding a purchase. The user must confirm before it is saved. The result tells you nothing about the new budget until confirmed.";
    public override AiPermissionKey Permission => AiPermissionKey.AddPurchase;
    public override JsonObject Schema => Tools.Schema.Object(new[]
    {
        Tools.Schema.Str("product", "Product name"), Tools.Schema.Str("category", "Budget category name or key"),
        Tools.Schema.Num("unit_price", "Price per unit in the budget currency"), Tools.Schema.Int("quantity", "Quantity, default 1"),
        Tools.Schema.Str("status", "Purchase status, default Planned", "Planned", "Ordered", "Purchased", "Received", "Cancelled"),
        Tools.Schema.Str("seller", "Seller"), Tools.Schema.Str("notes", "Notes"), Tools.Schema.Str("date", "ISO date, default today")
    }, "product", "category", "unit_price");

    public override async Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var product = args.Str("product");
        if (product is null) return AiJson.Error("missing_argument", "product is required.");
        if (args.Dec("unit_price") is not { } price) return AiJson.Error("missing_argument", "unit_price is required.");
        var (cat, error) = await FindCategoryAsync(_budget, args.Str("category"), ct).ConfigureAwait(false);
        if (error is not null) return error;

        var model = new PurchaseEditModel
        {
            Product = product, CategoryId = cat!.Id, UnitPrice = price, Quantity = args.Int("quantity") ?? 1,
            Status = args.Enum<PurchaseStatus>("status") ?? PurchaseStatus.Planned, Seller = args.Str("seller"), Notes = args.Str("notes"),
            PurchaseDate = args.Date("date") ?? DateTime.Today
        };
        WhatIfResult whatIf;
        try { whatIf = await _budget.SimulatePurchaseAsync(model, ct).ConfigureAwait(false); }
        catch (Core.Common.DomainException ex) { return AiJson.Error("invalid", ex.ErrorKey); }

        var cur = whatIf.Before.Currency;
        var fields = new List<ActionField>
        {
            new("AiAction.Product", model.Product),
            new("AiAction.Category", cat.Name ?? cat.Key),
            new("AiAction.UnitPrice", AiJson.Money(model.UnitPrice, cur)),
            new("AiAction.Quantity", model.Quantity.ToString()),
            new("AiAction.Status", model.Status.ToString()),
            new("AiAction.Total", AiJson.Money(whatIf.PurchaseTotal, cur)),
            new("AiAction.PlannedAfter", AiJson.Money(whatIf.After.TotalPlanned, cur)),
            new("AiAction.RemainingAfter", AiJson.Money(whatIf.After.Remaining, cur))
        };
        var action = new PendingAction(Name, Permission, "AiAction.AddPurchase", fields,
            async c => await _budget.SavePurchaseAsync(model, ActivityActor.Ai, c).ConfigureAwait(false),
            $"add_purchase: {model.Product} x{model.Quantity}", whatIf.BudgetImpact, cur);
        return Pending(action);
    }
}

internal sealed class EditPurchaseTool : WriteTool
{
    private readonly IBudgetService _budget;
    public EditPurchaseTool(IBudgetService budget) { _budget = budget; }
    public override string Name => "edit_purchase";
    public override string Description => "Propose changes to an existing purchase (identify by id or product name). The user must confirm.";
    public override AiPermissionKey Permission => AiPermissionKey.EditPurchase;
    public override JsonObject Schema => Tools.Schema.Object(new[]
    {
        Tools.Schema.Str("id", "Purchase id (GUID)"), Tools.Schema.Str("product", "Existing product name (if id unknown)"),
        Tools.Schema.Str("new_product", "New product name"), Tools.Schema.Str("category", "New category"),
        Tools.Schema.Num("unit_price", "New unit price"), Tools.Schema.Int("quantity", "New quantity"),
        Tools.Schema.Str("status", "New status", "Planned", "Ordered", "Purchased", "Received", "Cancelled"),
        Tools.Schema.Str("seller", "New seller"), Tools.Schema.Str("notes", "New notes")
    });

    public override async Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var (p, error) = await FindPurchaseAsync(_budget, args, ct).ConfigureAwait(false);
        if (error is not null) return error;

        var model = new PurchaseEditModel
        {
            Id = p!.Id, Product = p.Product, CategoryId = p.CategoryId, UnitPrice = p.UnitPrice, Quantity = p.Quantity,
            PurchaseDate = p.PurchaseDate, Status = p.Status, Seller = p.Seller, Notes = p.Notes
        };
        var fields = new List<ActionField> { new("AiAction.Product", p.Product) };
        void Change(string label, string before, string after) { if (before != after) fields.Add(new ActionField(label, $"{before} -> {after}")); }

        if (args.Str("new_product") is { } np) { Change("AiAction.NewName", model.Product, np); model.Product = np; }
        if (args.Str("category") is { } catQuery)
        {
            var (cat, catError) = await FindCategoryAsync(_budget, catQuery, ct).ConfigureAwait(false);
            if (catError is not null) return catError;
            Change("AiAction.Category", p.Category?.Name ?? p.Category?.Key ?? "", cat!.Name ?? cat.Key);
            model.CategoryId = cat.Id;
        }
        if (args.Dec("unit_price") is { } price) { Change("AiAction.UnitPrice", model.UnitPrice.ToString("0.00"), price.ToString("0.00")); model.UnitPrice = price; }
        if (args.Int("quantity") is { } qty) { Change("AiAction.Quantity", model.Quantity.ToString(), qty.ToString()); model.Quantity = qty; }
        if (args.Enum<PurchaseStatus>("status") is { } st) { Change("AiAction.Status", model.Status.ToString(), st.ToString()); model.Status = st; }
        if (args.Str("seller") is { } seller) { Change("AiAction.Seller", model.Seller ?? "-", seller); model.Seller = seller; }
        if (args.Str("notes") is { } notes) { Change("AiAction.Notes", model.Notes ?? "-", notes); model.Notes = notes; }
        if (fields.Count == 1) return AiJson.Error("no_changes", "Nothing to change.");

        var summary = await _budget.GetSummaryAsync(ct).ConfigureAwait(false);
        var oldAmount = p.Total;
        var newAmount = model.UnitPrice * model.Quantity;
        decimal? impact = newAmount != oldAmount ? -(newAmount - oldAmount) : null;
        var action = new PendingAction(Name, Permission, "AiAction.EditPurchase", fields,
            async c => await _budget.SavePurchaseAsync(model, ActivityActor.Ai, c).ConfigureAwait(false),
            $"edit_purchase: {p.Product}", impact, summary.Currency);
        return Pending(action);
    }
}

internal sealed class DeletePurchaseTool : WriteTool
{
    private readonly IBudgetService _budget;
    public DeletePurchaseTool(IBudgetService budget) { _budget = budget; }
    public override string Name => "delete_purchase";
    public override string Description => "Propose deleting a purchase (identify by id or product name). Destructive; the user must confirm twice.";
    public override AiPermissionKey Permission => AiPermissionKey.DeletePurchase;
    public override JsonObject Schema => Tools.Schema.Object(new[] { Tools.Schema.Str("id", "Purchase id (GUID)"), Tools.Schema.Str("product", "Product name (if id unknown)") });

    public override async Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var (p, error) = await FindPurchaseAsync(_budget, args, ct).ConfigureAwait(false);
        if (error is not null) return error;
        var summary = await _budget.GetSummaryAsync(ct).ConfigureAwait(false);
        var fields = new List<ActionField>
        {
            new("AiAction.Product", p!.Product), new("AiAction.Quantity", p.Quantity.ToString()),
            new("AiAction.Status", p.Status.ToString()), new("AiAction.Total", AiJson.Money(p.Total, summary.Currency))
        };
        var impact = BudgetCalculator.CountsAsPlanned(p.Status) ? p.Total : (decimal?)null;
        var id = p.Id;
        var action = new PendingAction(Name, Permission, "AiAction.DeletePurchase", fields,
            async c => await _budget.DeletePurchaseAsync(id, ActivityActor.Ai, c).ConfigureAwait(false),
            $"delete_purchase: {p.Product}", impact, summary.Currency, isDestructive: true);
        return Pending(action);
    }
}

internal sealed class UpdateMemberTool : WriteTool
{
    private readonly IMemberService _members;
    public UpdateMemberTool(IMemberService members) { _members = members; }
    public override string Name => "update_member";
    public override string Description => "Propose changes to a member: status, responsibilities, notes, extra skills or areas. The user must confirm.";
    public override AiPermissionKey Permission => AiPermissionKey.EditMember;
    public override JsonObject Schema => Tools.Schema.Object(new[]
    {
        Tools.Schema.Str("id", "Member id (GUID)"), Tools.Schema.Str("name", "Member name (if id unknown)"),
        Tools.Schema.Str("status", "New status name or key"), Tools.Schema.Str("responsibilities", "New responsibilities text"),
        Tools.Schema.Str("notes", "New notes text"), Tools.Schema.StrArray("add_skills", "Skills to add"), Tools.Schema.StrArray("add_areas", "Areas to add")
    });

    public override async Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var found = await MemberLookup.FindAsync(_members, args, ct).ConfigureAwait(false);
        if (found.Error is not null) return found.Error;
        var m = (await _members.GetAsync(found.Member!.Id, ct).ConfigureAwait(false))!;
        var statuses = await _members.ListStatusesAsync(ct).ConfigureAwait(false);
        var areas = await _members.ListAreasAsync(ct).ConfigureAwait(false);

        var model = new MemberEditModel
        {
            Id = m.Id, FullName = m.FullName, Email = m.Email, Phone = m.Phone, AvatarPath = m.AvatarPath, StatusId = m.StatusId,
            JoinDate = m.JoinDate, Responsibilities = m.Responsibilities, Notes = m.Notes,
            AreaIds = m.MemberAreas.Select(a => a.AreaId).ToList(), SkillNames = m.MemberSkills.Select(s => s.Skill!.Name).ToList(),
            CustomFields = m.CustomFields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)).ToList()
        };
        var fields = new List<ActionField> { new("AiAction.Member", m.FullName) };

        if (args.Str("status") is { } sq)
        {
            var st = statuses.FirstOrDefault(x => GetMembersTool.Matches(x.Key, x.Name, sq));
            if (st is null) return AiJson.Error("unknown_status", "No such status.", statuses.Select(x => x.Name ?? x.Key));
            if (st.Id != model.StatusId) { fields.Add(new("AiAction.Status", $"{m.Status?.Name ?? m.Status?.Key} -> {st.Name ?? st.Key}")); model.StatusId = st.Id; }
        }
        if (args.Str("responsibilities") is { } resp) { fields.Add(new("AiAction.Responsibilities", AiJson.Trunc(resp, 200))); model.Responsibilities = resp; }
        if (args.Str("notes") is { } notes) { fields.Add(new("AiAction.Notes", AiJson.Trunc(notes, 200))); model.Notes = notes; }
        var newSkills = args.StrList("add_skills").Where(s => !model.SkillNames.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
        if (newSkills.Count > 0) { fields.Add(new("AiAction.AddSkills", string.Join(", ", newSkills))); model.SkillNames.AddRange(newSkills); }
        var addAreas = new List<string>();
        foreach (var aq in args.StrList("add_areas"))
        {
            var area = areas.FirstOrDefault(x => GetMembersTool.Matches(x.Key, x.Name, aq));
            if (area is null) return AiJson.Error("unknown_area", "No such area.", areas.Select(x => x.Name ?? x.Key));
            if (!model.AreaIds.Contains(area.Id)) { model.AreaIds.Add(area.Id); addAreas.Add(area.Name ?? area.Key); }
        }
        if (addAreas.Count > 0) fields.Add(new("AiAction.AddAreas", string.Join(", ", addAreas)));
        if (fields.Count == 1) return AiJson.Error("no_changes", "Nothing to change.");

        var action = new PendingAction(Name, Permission, "AiAction.UpdateMember", fields,
            async c => await _members.SaveAsync(model, ActivityActor.Ai, c).ConfigureAwait(false), $"update_member: {m.FullName}");
        return Pending(action);
    }
}

internal sealed class UpdateApplicationStatusTool : WriteTool
{
    private readonly IApplicationService _apps;
    public UpdateApplicationStatusTool(IApplicationService apps) { _apps = apps; }
    public override string Name => "update_application_status";
    public override string Description => "Propose changing an application's status. Only when the user explicitly asks for it; never decide acceptance yourself. The user must confirm.";
    public override AiPermissionKey Permission => AiPermissionKey.ChangeApplicationStatus;
    public override JsonObject Schema => Tools.Schema.Object(new[]
    {
        Tools.Schema.Str("id", "Application id (GUID)"), Tools.Schema.Str("applicant", "Applicant name (if id unknown)"),
        Tools.Schema.Str("status", "New status", "Pending", "Reviewing", "Accepted", "Rejected", "Maybe")
    }, "status");

    public override async Task<AiToolOutcome> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        if (args.Enum<ApplicationStatus>("status") is not { } status) return AiJson.Error("missing_argument", "A valid status is required.");
        TeamApplication? app;
        if (args.Guid("id") is { } id) app = await _apps.GetAsync(id, ct).ConfigureAwait(false);
        else if (args.Str("applicant") is { } name)
        {
            var page = await _apps.QueryAsync(new ApplicationQuery { Search = name, PageSize = 20 }, ct).ConfigureAwait(false);
            var matches = page.Items.Where(a => a.ApplicantName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1) return AiJson.Error("ambiguous", "Several applicants match; use the id.", matches.Select(a => new { id = a.Id, name = a.ApplicantName }));
            app = matches.FirstOrDefault();
        }
        else return AiJson.Error("missing_argument", "Provide id or applicant.");
        if (app is null) return AiJson.Error("not_found", "No such application.");

        var appId = app.Id;
        var fields = new List<ActionField>
        {
            new("AiAction.Applicant", app.ApplicantName), new("AiAction.Status", $"{app.Status} -> {status}")
        };
        var action = new PendingAction(Name, Permission, "AiAction.UpdateApplicationStatus", fields,
            async c => await _apps.SetStatusAsync(appId, status, ActivityActor.Ai, c).ConfigureAwait(false),
            $"update_application_status: {app.ApplicantName} -> {status}");
        return Pending(action);
    }
}
