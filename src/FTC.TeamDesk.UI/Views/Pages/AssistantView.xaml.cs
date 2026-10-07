using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FTC.TeamDesk.UI.ViewModels.Assistant;

namespace FTC.TeamDesk.UI.Views.Pages;

public partial class AssistantView : UserControl
{
    public AssistantView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is AssistantViewModel vm)
                vm.Messages.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() => Scroller.ScrollToEnd()));
        };
    }

    /// <summary>Enter sends, Shift+Enter inserts a new line.</summary>
    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        if (DataContext is AssistantViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
