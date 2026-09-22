using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WDesk.Core;

namespace WDesk.Widgets.Music.Style
{
    public abstract class StyleBase : IStyleBuilder
    {
        public abstract FrameworkElement Build(PlacedWidget instance);

        protected static Border BuildBackground(string mode, int opacity, double cornerRadius)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(cornerRadius),
                ClipToBounds = true
            };

            byte alpha = (byte)(Math.Clamp(opacity, 0, 100) * 255 / 100);

            if (mode == "transparent")
            {
                border.Background = Brushes.Transparent;
            }
            else if (mode == "glass")
            {
                border.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x20, 0x20, 0x28));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                border.BorderThickness = new Thickness(1);
            }
            else if (mode == "acrylic")
            {
                border.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x14, 0x14, 0x1C));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                border.BorderThickness = new Thickness(1);
            }
            else if (mode == "gradient")
            {
                var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                g.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 0x2A, 0x2A, 0x3A), 0));
                g.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 0x1A, 0x1A, 0x24), 1));
                border.Background = g;
            }
            else
            {
                var baseColor = GetThemeColor("WidgetBg", Color.FromRgb(0x1E, 0x1E, 0x22));
                border.Background = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
            }

            return border;
        }

        protected static Effect BuildShadow(bool enabled, int intensity)
        {
            if (!enabled || intensity <= 0) return null;
            return new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = intensity * 2,
                ShadowDepth = intensity / 4.0,
                Opacity = 0.4,
                Direction = 270
            };
        }

        protected static Color ResolveAccentColor(string accentSetting)
        {
            if (!string.IsNullOrEmpty(accentSetting) && accentSetting != "auto")
                return ParseColor(accentSetting, Color.FromRgb(0x3B, 0x82, 0xF6));
            return GetThemeColor("WidgetAccent", Color.FromRgb(0x3B, 0x82, 0xF6));
        }

        protected static Color GetThemeColor(string key, Color fallback)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return fallback;
                var brush = app.TryFindResource(key) as SolidColorBrush;
                if (brush != null) return brush.Color;
                if (app.TryFindResource(key) is Color)
                    return (Color)app.TryFindResource(key);
            }
            catch { }
            return fallback;
        }

        protected static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return fallback;
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch { return fallback; }
        }

        protected static string GetSetting(PlacedWidget instance, string key, string defaultVal)
        {
            if (instance.Settings.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                return val;
            return defaultVal;
        }

        protected static bool GetBool(PlacedWidget instance, string key, bool defaultVal)
        {
            return GetSetting(instance, key, defaultVal ? "true" : "false") == "true";
        }

        protected static int GetInt(PlacedWidget instance, string key, int defaultVal)
        {
            return int.TryParse(GetSetting(instance, key, defaultVal.ToString()), out var v) ? v : defaultVal;
        }
    }
}