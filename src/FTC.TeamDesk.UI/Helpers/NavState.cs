using System.Windows;

namespace FTC.TeamDesk.UI.Helpers;

/// <summary>Attached flag so the sidebar button template can show the active indicator from a typed trigger.</summary>
public static class NavState
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(NavState), new PropertyMetadata(false));
    public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);
}
