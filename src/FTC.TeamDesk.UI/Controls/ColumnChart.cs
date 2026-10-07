using System.Windows;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Controls;

public class ColumnChart : ChartBase
{
    public static readonly DependencyProperty SingleColorProperty = DependencyProperty.Register(nameof(SingleColor), typeof(bool), typeof(ColumnChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowValuesProperty = DependencyProperty.Register(nameof(ShowValues), typeof(bool), typeof(ColumnChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private int _hover = -1;
    private readonly List<Rect> _hit = new();

    public bool SingleColor { get => (bool)GetValue(SingleColorProperty); set => SetValue(SingleColorProperty, value); }
    public bool ShowValues { get => (bool)GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }

    protected override Size MeasureOverride(Size s) => new(double.IsInfinity(s.Width) ? 320 : s.Width, double.IsInfinity(s.Height) ? 220 : s.Height);

    protected override void Render(DrawingContext dc)
    {
        var data = Data();
        _hit.Clear();
        if (data.Count == 0 || data.All(d => d.Value == 0)) { DrawEmpty(dc); return; }

        var muted = ThemeBrush("TextMutedBrush");
        var (max, step) = NiceScale(data.Max(d => d.Value));
        const double left = 40, bottom = 28, top = 10, right = 8;
        var plot = new Rect(left, top, Math.Max(10, ActualWidth - left - right), Math.Max(10, ActualHeight - top - bottom));
        var gridPen = new Pen(ThemeBrush("BorderBrush"), 1);
        var ticks = 0;

        for (var v = 0.0; v <= max + step / 2 && step > 0 && ticks++ < 60; v += step)
        {
            var y = plot.Bottom - v / max * plot.Height;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var t = Text(Compact(v), 11, muted);
            dc.DrawText(t, new Point(plot.Left - 6 - t.Width, y - t.Height / 2));
        }

        var slot = plot.Width / data.Count;
        var barW = Math.Min(48, slot * 0.6);
        for (var i = 0; i < data.Count; i++)
        {
            var h = data[i].Value / max * plot.Height;
            var x = plot.Left + slot * i + (slot - barW) / 2;
            var rect = new Rect(x, plot.Bottom - h, barW, Math.Max(h, data[i].Value > 0 ? 1 : 0));
            _hit.Add(new Rect(plot.Left + slot * i, plot.Top, slot, plot.Height));
            var brush = SeriesBrush(SingleColor ? 0 : data[i].ColorIndex);
            dc.DrawRoundedRectangle(brush, null, rect, 3, 3);
            if (i == _hover) dc.DrawRoundedRectangle(null, new Pen(ThemeBrush("TextBrush"), 1.5), rect, 3, 3);

            var label = Text(data[i].Label, 11, muted);
            label.MaxTextWidth = Math.Max(20, slot - 4);
            label.MaxLineCount = 1;
            dc.DrawText(label, new Point(plot.Left + slot * i + (slot - Math.Min(label.Width, label.MaxTextWidth)) / 2, plot.Bottom + 6));
            if (ShowValues && slot >= 44 && data[i].Value > 0)
            {
                var v = Text(Compact(data[i].Value), 11, ThemeBrush("TextSecondaryBrush"), FontWeights.SemiBold);
                dc.DrawText(v, new Point(x + (barW - v.Width) / 2, rect.Top - v.Height - 2));
            }
        }
    }

    protected override void OnHover(Point? position)
    {
        var idx = -1;
        if (position is { } p) for (var i = 0; i < _hit.Count; i++) if (_hit[i].Contains(p)) { idx = i; break; }
        if (idx == _hover) return;
        _hover = idx;
        var data = Data();
        if (idx >= 0 && idx < data.Count) ShowTip($"{data[idx].Label}: {data[idx].Text}"); else HideTip();
        InvalidateVisual();
    }
}
