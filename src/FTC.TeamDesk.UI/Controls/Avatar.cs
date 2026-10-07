using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FTC.TeamDesk.UI.Converters;

namespace FTC.TeamDesk.UI.Controls;

/// <summary>Round member avatar: photo when available, otherwise initials on a deterministic color.</summary>
public class Avatar : Border
{
    private static readonly AvatarBrushConverter Colors = new();
    private readonly TextBlock _text = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };

    public static readonly DependencyProperty InitialsProperty = DependencyProperty.Register(nameof(Initials), typeof(string), typeof(Avatar), new PropertyMetadata("", (d, _) => ((Avatar)d).Update()));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(int), typeof(Avatar), new PropertyMetadata(0, (d, _) => ((Avatar)d).Update()));
    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(Avatar), new PropertyMetadata(null, (d, _) => ((Avatar)d).Update()));
    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(Avatar), new PropertyMetadata(36.0, (d, _) => ((Avatar)d).Update()));

    public Avatar() { Child = _text; Update(); }

    public string Initials { get => (string)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    public int Tone { get => (int)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public string? ImagePath { get => (string?)GetValue(ImagePathProperty); set => SetValue(ImagePathProperty, value); }
    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    private void Update()
    {
        Width = Height = Diameter;
        CornerRadius = new CornerRadius(Diameter / 2);
        _text.FontSize = Diameter * 0.38;
        _text.Text = Initials;
        Brush? photo = null;
        if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // do not keep the file locked
                bmp.DecodePixelWidth = (int)(Diameter * 2);
                bmp.UriSource = new Uri(ImagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                photo = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
            }
            catch (Exception) { photo = null; } // broken image file: fall back to initials
        }
        Background = photo ?? (Brush)Colors.Convert(Tone, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        _text.Visibility = photo is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
