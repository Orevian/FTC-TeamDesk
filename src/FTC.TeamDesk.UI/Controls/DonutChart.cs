using System.Windows;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Controls;

public class DonutChart : ChartBase
{
    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(nameof(CenterText), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CenterCaptionProperty = DependencyProperty.Register(nameof(CenterCaption), typeof(string), typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private int _hover = -1;
    private readonly List<(double Start, double Sweep)> _arcs = new();

    public string CenterText { get => (string)GetValue(CenterTextProperty); set => SetValue(CenterTextProperty, value); }
    public string CenterCaption { get => (string)GetValue(CenterCaptionProperty); set => SetValue(CenterCaptionProperty, value); }

    protected override Size MeasureOverride(Size s)
    {
        var side = double.IsInfinity(s.Width) ? (double.IsInfinity(s.Height) ? 180 : s.Height) : (double.IsInfinity(s.Height) ? s.Width : Math.Min(s.Width, s.Height));
        return new Size(side, side);
    }

    protected override void Render(DrawingContext dc)
    {
        var data = Data().Where(d => d.Value > 0).ToList();
        _arcs.Clear();
        var total = data.Sum(d => d.Value);
        if (total <= 0 || ActualWidth < 20) { DrawEmpty(dc); return; }

        var size = Math.Min(ActualWidth, ActualHeight);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var outer = size / 2 - 4;
        var thickness = Math.Max(14, outer * 0.28);
        var radius = outer - thickness / 2;
        var gap = data.Count > 1 ? 1.5 : 0;
        var angle = -90.0;

        for (var i = 0; i < data.Count; i++)
        {
            var sweep = data[i].Value / total * 360.0;
            _arcs.Add((angle, sweep));
            var drawSweep = Math.Max(0.5, sweep - gap);
            var pen = new Pen(SeriesBrush(data[i].ColorIndex), i == _hover ? thickness + 6 : thickness) { LineJoin = PenLineJoin.Round };
            if (sweep >= 359.9)
                dc.DrawEllipse(null, pen, center, radius, radius);
            else
                dc.DrawGeometry(null, pen, Arc(center, radius, angle + gap / 2, drawSweep));
            angle += sweep;
        }

        var main = Text(_hover >= 0 && _hover < data.Count ? Compact(data[_hover].Value) : CenterText, outer * 0.32, ThemeBrush("TextBrush"), FontWeights.SemiBold);
        dc.DrawText(main, new Point(center.X - main.Width / 2, center.Y - main.Height / 2 - (string.IsNullOrEmpty(CenterCaption) ? 0 : 7)));
        var cap = _hover >= 0 && _hover < data.Count ? data[_hover].Label : CenterCaption;
        if (!string.IsNullOrEmpty(cap))
        {
            var c = Text(cap, 12, ThemeBrush("TextMutedBrush"));
            c.MaxTextWidth = thickness * 4;
            c.MaxLineCount = 1;
            dc.DrawText(c, new Point(center.X - Math.Min(c.Width, c.MaxTextWidth) / 2, center.Y + main.Height / 2 - 7));
        }
    }

    private static Geometry Arc(Point c, double r, double startDeg, double sweepDeg)
    {
        Point P(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(startDeg), false, false);
            ctx.ArcTo(P(startDeg + sweepDeg), new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    protected override void OnHover(Point? position)
    {
        var data = Data().Where(d => d.Value > 0).ToList();
        var idx = -1;
        if (position is { } p && data.Count > 0 && _arcs.Count == data.Count)
        {
            var c = new Point(ActualWidth / 2, ActualHeight / 2);
            var dx = p.X - c.X; var dy = p.Y - c.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var outer = Math.Min(ActualWidth, ActualHeight) / 2 - 4;
            if (dist <= outer + 6 && dist >= outer * 0.6)
            {
                var deg = Math.Atan2(dy, dx) * 180 / Math.PI;
                if (deg < -90) deg += 360;
                for (var i = 0; i < _arcs.Count; i++)
                    if (deg >= _arcs[i].Start && deg < _arcs[i].Start + _arcs[i].Sweep) { idx = i; break; }
            }
        }
        if (idx == _hover) return;
        _hover = idx;
        if (idx >= 0)
        {
            var total = data.Sum(d => d.Value);
            ShowTip($"{data[idx].Label}: {data[idx].Text} ({data[idx].Value / total:P0})");
        }
        else HideTip();
        InvalidateVisual();
    }
}
