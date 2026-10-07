using System.Windows;
using System.Windows.Controls;

namespace FTC.TeamDesk.UI.Controls;

/// <summary>Small colored square for chart legends; binds to the theme's Chart1..Chart8 brush so it follows theme changes.</summary>
public class ChartSwatch : Border
{
    public static readonly DependencyProperty ColorIndexProperty = DependencyProperty.Register(nameof(ColorIndex), typeof(int), typeof(ChartSwatch),
        new PropertyMetadata(0, (d, _) => ((ChartSwatch)d).Apply()));

    public ChartSwatch() { Width = 10; Height = 10; CornerRadius = new CornerRadius(2); VerticalAlignment = VerticalAlignment.Center; Apply(); }

    public int ColorIndex { get => (int)GetValue(ColorIndexProperty); set => SetValue(ColorIndexProperty, value); }

    private void Apply() => SetResourceReference(BackgroundProperty, ChartTheme.BrushKey(ColorIndex));
}
