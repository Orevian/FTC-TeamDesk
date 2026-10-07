using System.Windows;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Controls;

/// <summary>Single horizontal stacked bar (e.g. spent / committed / free budget) with hover tooltips.</summary>
public class SegmentBar : ChartBase
{
    private int _hover = -1;
    private readonly List<Rect> _hit = new();

    protected override Size MeasureOverride(Size s) => new(double.IsInfinity(s.Width) ? 300 : s.Width, 14);

    protected override void Render(DrawingContext dc)
    {
        var data = Data().Where(d => d.Value > 0).ToList();
        _hit.Clear();
        var track = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle(ThemeBrush("HoverBrush"), null, track, 6, 6);
        var total = data.Sum(d => d.Value);
        if (total <= 0) return;

        dc.PushClip(new RectangleGeometry(track, 6, 6));
        var x = 0.0;
        for (var i = 0; i < data.Count; i++)
        {
            var w = data[i].Value / total * ActualWidth;
            var r = new Rect(x, 0, Math.Max(w - (i < data.Count - 1 ? 2 : 0), 1), ActualHeight);
            dc.DrawRectangle(SeriesBrush(data[i].ColorIndex), null, r);
            _hit.Add(new Rect(x, 0, w, ActualHeight));
            x += w;
        }
        dc.Pop();
    }

    protected override void OnHover(Point? position)
    {
        var idx = -1;
        if (position is { } p) for (var i = 0; i < _hit.Count; i++) if (_hit[i].Contains(p)) { idx = i; break; }
        if (idx == _hover) return;
        _hover = idx;
        var data = Data().Where(d => d.Value > 0).ToList();
        if (idx >= 0 && idx < data.Count) ShowTip($"{data[idx].Label}: {data[idx].Text}"); else HideTip();
    }
}
