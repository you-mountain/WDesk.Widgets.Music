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
    public class Style2 : StyleBase
    {
        private Rectangle[] _bars = new Rectangle[0];
        private DispatcherTimer _renderTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 32);
            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var bgMode = GetSetting(instance, "bg_mode", "glass");
            var bgOpacity = GetInt(instance, "bg_opacity", 80);
            var cornerRadius = GetInt(instance, "corner_radius", 16);
            var accentSetting = GetSetting(instance, "accent_color", "auto");
            var showLabel = GetBool(instance, "show_label", true);
            var showShadow = GetBool(instance, "show_shadow", true);

            var accentColor = ResolveAccentColor(accentSetting);
            var textBrush = new SolidColorBrush(GetThemeColor("WidgetTextPrimary", Colors.White));
            var txtColor = ((SolidColorBrush)textBrush).Color;
            var mutedBrush = new SolidColorBrush(Color.FromArgb(0xA0, txtColor.R, txtColor.G, txtColor.B));

            var engine = AudioCaptureService.Instance;
            engine.Start();

            var root = new Grid();

            var bg = BuildBackground(bgMode, bgOpacity, cornerRadius);
            bg.Effect = BuildShadow(showShadow, 12);
            root.Children.Add(bg);

            var content = new Grid { Margin = new Thickness(16, 14, 16, 14) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(content);

            if (showLabel)
            {
                var label = new TextBlock
                {
                    Text = "NOW PLAYING",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = mutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                Grid.SetRow(label, 0);
                content.Children.Add(label);
            }

            var waveGrid = new UniformGrid
            {
                Rows = 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(0, 0)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B), 0));
            gradient.GradientStops.Add(new GradientStop(accentColor, 0.5));
            gradient.GradientStops.Add(new GradientStop(Colors.White, 1));

            _bars = new Rectangle[barsCount];

            for (int i = 0; i < barsCount; i++)
            {
                var bar = new Rectangle
                {
                    Height = 4,
                    Width = 3,
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Fill = gradient,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(1, 0, 1, 0)
                };
                _bars[i] = bar;
                waveGrid.Children.Add(bar);
            }

            Grid.SetRow(waveGrid, 1);
            content.Children.Add(waveGrid);

            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = engine.Spectrum;
                var spectrumLen = spectrum.Length;
                var maxHeight = 70.0;

                for (int i = 0; i < _bars.Length; i++)
                {
                    var srcIdx = (int)(i / (double)_bars.Length * spectrumLen);
                    if (srcIdx < 0) srcIdx = 0;
                    if (srcIdx >= spectrumLen) srcIdx = spectrumLen - 1;

                    var amplitude = spectrum[srcIdx] * sensitivity;
                    if (amplitude < 0) amplitude = 0;
                    if (amplitude > 1) amplitude = 1;

                    var barHeight = 4 + amplitude * maxHeight;
                    _bars[i].Height = Math.Min(barHeight, maxHeight);
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