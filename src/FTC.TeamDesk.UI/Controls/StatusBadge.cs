using System.Windows;
using System.Windows.Controls;

namespace FTC.TeamDesk.UI.Controls;

/// <summary>Pill showing a status text. Color comes from the theme via <see cref="Tone"/> (default style in Controls.xaml).</summary>
public class StatusBadge : Control
{
    static StatusBadge() => DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusBadge), new FrameworkPropertyMetadata(typeof(StatusBadge)));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(StatusBadge), new PropertyMetadata(StatusTone.Neutral));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
}
