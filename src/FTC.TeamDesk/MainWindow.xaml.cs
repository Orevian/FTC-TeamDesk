using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FTC.TeamDesk.Core.Abstractions.Services;

namespace FTC.TeamDesk;

public partial class MainWindow : Window
{
    private readonly ISettingsService? _settings;
    private readonly IVaultService? _vault;

    public MainWindow(ISettingsService settings, IVaultService vault)
    {
        InitializeComponent();
        _settings = settings; _vault = vault;
        LoadIcon();

        MinButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxButton.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        CloseButton.Click += (_, _) => Close();
        StateChanged += OnStateChanged;
        SourceInitialized += (_, _) => ApplyRoundedCorners();
        Loaded += (_, _) => Focus();
    }

    /// <summary>Uses Assets/icon.ico (when present at build time) for the window, taskbar and title bar.</summary>
    private void LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.Absolute);
            var decoder = new IconBitmapDecoder(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            Icon = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            var small = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - 32)).First();
            TitleIcon.Source = small;
            TitleIcon.Visibility = Visibility.Visible;
            _ = frame;
        }
        catch (Exception)
        {
            // No icon.ico was supplied at build time: run without a custom icon.
            TitleIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var max = WindowState == WindowState.Maximized;
        MaxButton.Content = max ? "" : "";
        // A borderless window that is maximized overhangs the screen by the resize border; compensate.
        Root.Margin = max ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + 1) : new Thickness(0);
        if (WindowState == WindowState.Minimized && _settings?.Current.LockVaultOnMinimize == true) _vault?.Lock("minimize");
    }

    private void ApplyRoundedCorners()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var pref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref pref, sizeof(int));
        }
        catch (Exception) { /* Windows 10: unsupported attribute, ignore */ }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
