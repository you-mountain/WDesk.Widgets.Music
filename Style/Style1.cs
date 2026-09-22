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
    public class Style1 : StyleBase
    {
        private Rectangle[] _bars = new Rectangle[0];
        private DispatcherTimer _renderTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 32);
            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var bgMode = GetSetting(instance, "bg_mode", "solid");
            var bgOpacity = GetInt(instance, "bg_opacity", 100);
            var cornerRadius = GetInt(instance, "corner_radius", 16);
            var accentSetting = GetSetting(instance, "accent_color", "auto");
            var showLabel = GetBool(instance, "show_label", true);
            var showShadow = GetBool(instance, "show_shadow", true);

            var accentColor = ResolveAccentColor(accentSetting);
            var accentBrush = new SolidColorBrush(accentColor);
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

            var eqGrid = new UniformGrid
            {
                Rows = 1,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            _bars = new Rectangle[barsCount];

            for (int i = 0; i < barsCount; i++)
            {
                var bar = new Rectangle
                {
                    Height = 4,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = accentBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(1, 0, 1, 0),
                    MinHeight = 4
                };
                _bars[i] = bar;
                eqGrid.Children.Add(bar);
            }

            Grid.SetRow(eqGrid, 1);
            content.Children.Add(eqGrid);

            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = engine.Spectrum;
                var spectrumLen = spectrum.Length;
                var maxHeight = 60.0;

                for (int i = 0; i < _bars.Length; i++)
                {
                    var srcIdx = (int)(i / (double)_bars.Length * spectrumLen);
                    if (srcIdx < 0) srcIdx = 0;
                    if (srcIdx >= spectrumLen) srcIdx = spectrumLen - 1;

                    var amplitude = spectrum[srcIdx] * sensitivity;
                    if (amplitude < 0) amplitude = 0;
                    if (amplitude > 1) amplitude = 1;

                    var targetHeight = 4 + amplitude * (maxHeight - 4);
                    var currentHeight = _bars[i].Height;
                    var newHeight = Math.Max(targetHeight, currentHeight * 0.7);

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