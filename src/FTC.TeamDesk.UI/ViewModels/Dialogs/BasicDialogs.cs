using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Dialogs;

public sealed class ConfirmDialogViewModel : DialogViewModel
{
    private readonly string _title, _confirm;
    private readonly bool _destructive;

    public ConfirmDialogViewModel(UiContext ui, string title, string message, string confirmText, bool destructive) : base(ui)
    { _title = title; Message = message; _confirm = confirmText; _destructive = destructive; }

    public override string Title => _title;
    public string Message { get; }
    public override string ConfirmText => _confirm;
    public override bool IsDestructive => _destructive;
    public override double Width => 440;
}
