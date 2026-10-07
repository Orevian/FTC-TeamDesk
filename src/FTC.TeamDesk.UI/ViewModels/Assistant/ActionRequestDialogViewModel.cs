using FTC.TeamDesk.AI.Tools;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Assistant;

public sealed record ActionFieldRow(string Label, string Value);

/// <summary>ACTION REQUEST: shows exactly what the AI wants to change; the user approves or rejects.</summary>
public sealed class ActionRequestDialogViewModel : DialogViewModel
{
    private readonly PendingAction _action;

    public ActionRequestDialogViewModel(UiContext ui, PendingAction action) : base(ui)
    {
        _action = action;
        Fields = action.Fields.Select(f => new ActionFieldRow(L.Get(f.LabelKey), f.Value)).ToList();
    }

    public override string Title => L["AiAction.Title"];
    public override string ConfirmText => L["AiAction.Approve"];
    public override string CancelText => L["AiAction.Reject"];
    public override bool IsDestructive => _action.IsDestructive;
    public override double Width => 540;
    public string ActionName => L[_action.TitleKey];
    public string Intro => L["AiAction.Intro"];
    public IReadOnlyList<ActionFieldRow> Fields { get; }
    public bool HasBudgetImpact => _action.BudgetImpact is not null;
    public string BudgetImpactLabel => L["AiAction.BudgetImpact"];
    public string BudgetImpact => _action.BudgetImpact is { } v ? Ui.Format.Money(v, _action.Currency ?? "USD") : "";
    public string Warning => _action.IsDestructive ? L["AiAction.DeleteWarning"] : "";
    public bool HasWarning => _action.IsDestructive;
}
