using System.Windows;
using System.Windows.Controls;

namespace FTC.TeamDesk.UI.Helpers;

/// <summary>
/// Two-way bindable password for PasswordBox. The password is held as a string only while the user is entering it;
/// view models clear it right after use. (PasswordBox.SecurePassword cannot be bound directly.)
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundChanged));
    private static readonly DependencyProperty UpdatingProperty = DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBoxHelper));

    public static string? GetBoundPassword(DependencyObject d) => (string?)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string? value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        if (!(bool)box.GetValue(UpdatingProperty) && box.Password != (string?)e.NewValue) box.Password = (string?)e.NewValue ?? string.Empty;
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}
