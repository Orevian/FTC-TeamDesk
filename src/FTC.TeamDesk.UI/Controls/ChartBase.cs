using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Controls;

public static class ChartTheme
{
    public static event Action? Changed;
    public static void NotifyChanged() => Changed?.Invoke();
    public static string BrushKey(int index) => $"Chart{(Math.Abs(index) % 8) + 1}Brush";
}

/// <summary>
/// Base for the hand-drawn charts: theme brush lookup, redraw on data/theme/size change, hover tooltip and text helpers.
/// Charts draw only what they need (no chart library), which keeps the app small and fully theme-aware.
/// </summary>
public abstract class ChartBase : FrameworkElement
{
    private readonly ToolTip _tip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, IsHitTestVisible = false };
    private INotifyCollectionChanged? _observed;

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(ChartBase),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((ChartBase)d).OnItemsChanged(e.OldValue as IEnumerable, e.NewValue as IEnumerable)));
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(ChartBase),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    protected ChartBase()
    {
        SnapsToDevicePixels = true;
        Loaded += (_, _) => ChartTheme.Changed += OnThemeChanged;
        Unloaded += (_, _) => { ChartTheme.Changed -= OnThemeChanged; HideTip(); };
        MouseLeave += (_, _) => { OnHover(null); HideTip(); };
        MouseMove += (_, e) => OnHover(e.GetPosition(this));
    }

    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    protected virtual void OnItemsChanged(IEnumerable? oldV, IEnumerable? newV)
    {
        if (_observed is not null) _observed.CollectionChanged -= OnCollectionChanged;
        _observed = newV as INotifyCollectionChanged;
        if (_observed is not null) _observed.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    private void OnThemeChanged() => Dispatcher.BeginInvoke(new Action(InvalidateVisual));

    /// <summary>Draws the chart. Wrapped by <see cref="OnRender"/> so a drawing error can never repeat on every frame (which would freeze the UI).</summary>
    protected abstract void Render(DrawingContext dc);

    protected sealed override void OnRender(DrawingContext drawingContext)
    {
        try { Render(drawingContext); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArithmeticException or OverflowException or NotSupportedException)
        {
            Diag.Mark("chart render failed: " + ex.GetType().Name + " in " + GetType().Name);
        }
    }

    protected abstract void OnHover(Point? position);

    protected void ShowTip(string text)
    {
        _tip.Content = text;
        _tip.IsOpen = true;
    }

    protected void HideTip() => _tip.IsOpen = false;

    protected Brush ThemeBrush(string key, Brush? fallback = null) => (TryFindResource(key) as Brush) ?? fallback ?? Brushes.Gray;
    protected Brush SeriesBrush(int index) => ThemeBrush(ChartTheme.BrushKey(index));

    protected Typeface UiTypeface => new(TextBlock.GetFontFamily(this), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    protected FormattedText Text(string text, double size, Brush brush, FontWeight? weight = null)
    {
        var face = new Typeface(TextBlock.GetFontFamily(this), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection, face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            Trimming = TextTrimming.CharacterEllipsis
        };
    }

    protected void DrawEmpty(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(EmptyText)) return;
        var t = Text(EmptyText, 13, ThemeBrush("TextMutedBrush"));
        dc.DrawText(t, new Point((ActualWidth - t.Width) / 2, (ActualHeight - t.Height) / 2));
    }

    /// <summary>Picks a "nice" axis maximum and step so gridlines fall on round numbers.</summary>
    protected static (double Max, double Step) NiceScale(double max, int ticks = 4)
    {
        if (!double.IsFinite(max) || max <= 0) return (1, 0.25);
        var raw = max / ticks;
        var pow = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var n = raw / pow;
        var step = (n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10) * pow;
        var top = Math.Ceiling(max / step) * step;
        return double.IsFinite(top) && step > 0 ? (top, step) : (1, 0.25);
    }

    protected static string Compact(double v)
    {
        var a = Math.Abs(v);
        if (a >= 1_000_000) return (v / 1_000_000).ToString("0.#", CultureInfo.CurrentCulture) + "M";
        if (a >= 1_000) return (v / 1_000).ToString("0.#", CultureInfo.CurrentCulture) + "k";
        return v.ToString("0.##", CultureInfo.CurrentCulture);
    }

    protected IReadOnlyList<ChartDatum> Data() => Items?.OfType<ChartDatum>().ToList() ?? new List<ChartDatum>();
}
