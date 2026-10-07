using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FTC.TeamDesk.UI.Converters;

/// <summary>true -> Collapsed, false -> Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>null / empty string -> Collapsed; anything else -> Visible. Set ConverterParameter=invert to flip.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var has = value switch { null => false, string s => s.Length > 0, System.Collections.ICollection c => c.Count > 0, _ => true };
        if (parameter is "invert") has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Parses "#RRGGBB" into a brush (used for user-defined status colors).</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try { if (value is string s && s.Length > 0) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s)); b.Freeze(); return b; } }
        catch (FormatException) { }
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Chart color index -> theme chart brush (resolved at conversion time).</summary>
public sealed class AvatarBrushConverter : IValueConverter
{
    private static readonly Brush[] Palette = new[] { "#2456D6", "#067647", "#7A3EC8", "#B54708", "#0E7490", "#B42318" }
        .Select(c => { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)); b.Freeze(); return (Brush)b; }).ToArray();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Palette[Math.Abs(value is int i ? i : 0) % Palette.Length];
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Formats a double 0..100 as "NN%".</summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double d ? $"{d:0}%" : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Bool -> chosen brush key resource lookups for danger/normal text (parameter "TrueKey|FalseKey").</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var keys = (parameter as string ?? "DangerBrush|TextBrush").Split('|');
        return Application.Current.TryFindResource(value is true ? keys[0] : keys[1]) ?? Brushes.Black;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Score 0..4 -> filled width fraction for the password strength meter.</summary>
public sealed class StrengthToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int s ? (s + 1) * 20.0 : 0.0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Localization key -> text (for items that are stored as keys in a list).</summary>
public sealed class LocKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string k ? FTC.TeamDesk.UI.Localization.LocalizationSource.Instance[k] : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Full name -> initials (live preview in the member editor).</summary>
public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => DisplayFormatter.Initials(value as string ?? string.Empty);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
