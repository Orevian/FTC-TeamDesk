using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Controls;

/// <summary>Multi-series line chart. The x axis is the sorted union of point labels (ISO dates / year-month sort correctly as text).</summary>
public class LineChart : ChartBase
{
    public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(LineChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(LineChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<(Point Pos, string Text)> _points = new();
    private int _hover = -1;

    public double MinValue { get => (double)GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    protected override Size MeasureOverride(Size s) => new(double.IsInfinity(s.Width) ? 320 : s.Width, double.IsInfinity(s.Height) ? 220 : s.Height);

    private List<LineSeriesData> Series() => Items switch
    {
        null => new(),
        IEnumerable e => e.OfType<LineSeriesData>().Where(s => s.Points.Count > 0).ToList()
    };

    protected override void Render(DrawingContext dc)
    {
        var series = Series();
        _points.Clear();
        if (series.Count == 0) { DrawEmpty(dc); return; }

        var muted = ThemeBrush("TextMutedBrush");
        var labels = series.SelectMany(s => s.Points.Select(p => p.Label)).Distinct().OrderBy(l => l, StringComparer.Ordinal).ToList();
        var allValues = series.SelectMany(s => s.Points.Select(p => p.Value)).ToList();
        var min = double.IsNaN(MinValue) ? Math.Min(0, allValues.Min()) : MinValue;
        var maxRaw = double.IsNaN(MaxValue) ? allValues.Max() : MaxValue;
        var (max, step) = double.IsNaN(MaxValue) ? NiceScale(Math.Max(maxRaw, min + 1)) : (MaxValue, Math.Max(1, (MaxValue - min) / 5));

        if (!double.IsFinite(max - min) || max - min <= 0) { DrawEmpty(dc); return; }
        const double left = 40, bottom = 28, top = 12, right = 16;
        var plot = new Rect(left, top, Math.Max(10, ActualWidth - left - right), Math.Max(10, ActualHeight - top - bottom));
        var gridPen = new Pen(ThemeBrush("BorderBrush"), 1);
        var ticks = 0;
        for (var v = min; v <= max + step / 2 && step > 0 && ticks++ < 60; v += step)
        {
            var y = plot.Bottom - (v - min) / (max - min) * plot.Height;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var t = Text(Compact(v), 11, muted);
            dc.DrawText(t, new Point(plot.Left - 6 - t.Width, y - t.Height / 2));
        }

        double X(int i) => labels.Count == 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * i / (labels.Count - 1);
        double Y(double v) => plot.Bottom - (v - min) / (max - min) * plot.Height;

        // x labels: thin out so they never overlap
        var maxLabels = Math.Max(2, (int)(plot.Width / 70));
        var every = (int)Math.Ceiling(labels.Count / (double)maxLabels);
        for (var i = 0; i < labels.Count; i += every)
        {
            var t = Text(labels[i], 11, muted);
            var x = Math.Clamp(X(i) - t.Width / 2, 0, Math.Max(0, ActualWidth - t.Width));
            dc.DrawText(t, new Point(x, plot.Bottom + 6));
        }

        foreach (var s in series)
        {
            var brush = SeriesBrush(s.ColorIndex);
            var ordered = s.Points.OrderBy(p => p.Label, StringComparer.Ordinal).ToList();
            var pts = ordered.Select(p => new Point(X(labels.IndexOf(p.Label)), Y(p.Value))).ToList();
            if (pts.Count > 1)
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(pts[0], false, false);
                    ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
                }
                g.Freeze();
                dc.DrawGeometry(null, new Pen(brush, 2.25) { LineJoin = PenLineJoin.Round }, g);
            }
            for (var i = 0; i < pts.Count; i++)
            {
                var idx = _points.Count;
                _points.Add((pts[i], $"{s.Name} • {ordered[i].Label}: {ordered[i].Text}"));
                var hot = idx == _hover;
                dc.DrawEllipse(ThemeBrush("SurfaceBrush"), new Pen(brush, 2), pts[i], hot ? 6 : 4, hot ? 6 : 4);
            }
        }
    }

    protected override void OnHover(Point? position)
    {
        var idx = -1;
        if (position is { } p)
        {
            var best = 14.0;
            for (var i = 0; i < _points.Count; i++)
            {
                var d = (_points[i].Pos - p).Length;
                if (d < best) { best = d; idx = i; }
            }
        }
        if (idx == _hover) return;
        _hover = idx;
        if (idx >= 0) ShowTip(_points[idx].Text); else HideTip();
        InvalidateVisual();
    }
}
