using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WDesk.Core;

namespace WDesk.Widgets.Music.Style
{
    public class Style3 : StyleBase
    {
        private Rectangle[] _bars = new Rectangle[0];
        private DispatcherTimer _renderTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 16);
            if (barsCount > 32) barsCount = 32;

            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var bgMode = GetSetting(instance, "bg_mode", "acrylic");
            var bgOpacity = GetInt(instance, "bg_opacity", 70);
            var cornerRadius = GetInt(instance, "corner_radius", 20);
            var accentSetting = GetSetting(instance, "accent_color", "auto");
            var showShadow = GetBool(instance, "show_shadow", true);

            var accentColor = ResolveAccentColor(accentSetting);
            var accentBrush = new SolidColorBrush(accentColor);

            var engine = AudioCaptureService.Instance;
            engine.Start();

            var root = new Grid();

            var bg = BuildBackground(bgMode, bgOpacity, cornerRadius);
            bg.Effect = BuildShadow(showShadow, 12);
            root.Children.Add(bg);

            var content = new Grid { Margin = new Thickness(12, 8, 12, 8) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(content);

            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x30, accentColor.R, accentColor.G, accentColor.B)),
                VerticalAlignment = VerticalAlignment.Center
            };

            iconBox.Child = new TextBlock
            {
                Text = "\uE8D6",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(iconBox, 0);
            content.Children.Add(iconBox);

            var eqGrid = new UniformGrid
            {
                Rows = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            _bars = new Rectangle[barsCount];

            for (int i = 0; i < barsCount; i++)
            {
                var bar = new Rectangle
                {
                    Height = 3,
                    Width = 2,
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = accentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0.5, 0, 0.5, 0)
                };
                _bars[i] = bar;
                eqGrid.Children.Add(bar);
            }

            Grid.SetColumn(eqGrid, 1);
            content.Children.Add(eqGrid);

            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = engine.Spectrum;
                var spectrumLen = spectrum.Length;
                var maxHeight = 28.0;

                for (int i = 0; i < _bars.Length; i++)
                {
                    var srcIdx = (int)(i / (double)_bars.Length * spectrumLen);
                    if (srcIdx < 0) srcIdx = 0;
                    if (srcIdx >= spectrumLen) srcIdx = spectrumLen - 1;

                    var amplitude = spectrum[srcIdx] * sensitivity;
                    if (amplitude < 0) amplitude = 0;
                    if (amplitude > 1) amplitude = 1;

                    var barHeight = 3 + amplitude * (maxHeight - 3);
                    var currentHeight = _bars[i].Height;
                    var newHeight = Math.Max(barHeight, currentHeight * 0.75);

                    _bars[i].Height = Math.Min(newHeight, maxHeight);
                }
            };
            _renderTimer.Start();

            root.Unloaded += (s, e) =>
            {
                if (_renderTimer != null)
                {
                    _renderTimer.Stop();
                    _renderTimer = null;
                }
            };

            return root;
        }
    }
}