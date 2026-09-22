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

        // ═══════════════════════════════════════════
        //  ★ Background — از WDesk core میاد
        // ═══════════════════════════════════════════
        protected static Border BuildBackground()
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true
            };
            border.SetResourceReference(Border.BackgroundProperty, "WidgetBg");
            return border;
        }

        // ★ Overload قدیمی — برای سازگاری
        protected static Border BuildBackground(string mode, int opacity, double cornerRadius)
        {
            return BuildBackground();
        }

        // ═══════════════════════════════════════════
        //  ★ Shadow — از WDesk core یا خالی
        // ═══════════════════════════════════════════
        protected static Effect BuildShadow(bool enabled, int intensity = 12)
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

        // ═══════════════════════════════════════════
        //  ★ Accent Color — از WDesk core یا cover art
        // ═══════════════════════════════════════════
        protected static Color ResolveAccentColor(PlacedWidget instance)
        {
            // ★ اگه cover color فعاله، از track info بگیر
            var useCoverColor = GetBool(instance, "use_cover_color", true);
            if (useCoverColor)
            {
                try
                {
                    var trackService = TrackInfoService.Instance;
                    if (trackService != null && trackService.CoverArt != null)
                    {
                        return trackService.DominantColor;
                    }
                }
                catch { }
            }

            // ★ fallback: WDesk accent
            return GetThemeColor("WidgetAccent", Color.FromRgb(0x3B, 0x82, 0xF6));
        }

        // ★ Overload قدیمی
        protected static Color ResolveAccentColor(string accentSetting)
        {
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