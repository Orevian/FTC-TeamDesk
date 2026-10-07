using System.Windows;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Controls;
using Microsoft.Win32;

namespace FTC.TeamDesk.UI.Services;

/// <summary>Swaps the color dictionary at runtime (index 0 of the application's merged dictionaries).</summary>
public sealed class ThemeService : IThemeService
{
    private ThemeMode _mode = ThemeMode.System;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (_mode == ThemeMode.System && e.Category == UserPreferenceCategory.General) Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(_mode)));
        };
    }

    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
        }
        catch (Exception) { return true; }
    }

    public bool IsDark { get; private set; }

    public void Apply(ThemeMode mode)
    {
        _mode = mode;
        IsDark = mode == ThemeMode.Dark || (mode == ThemeMode.System && !SystemUsesLightTheme());
        var uri = new Uri($"pack://application:,,,/FTC.TeamDesk.UI;component/Themes/Colors.{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;
        // The color dictionary is always the first merged dictionary; the rest reference it dynamically.
        if (merged.Count > 0) merged[0] = dict; else merged.Add(dict);
        ChartTheme.NotifyChanged();
        ThemeChanged?.Invoke(this, IsDark);
    }

    public event EventHandler<bool>? ThemeChanged;
}
