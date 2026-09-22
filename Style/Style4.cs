using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WDesk.Core;

namespace WDesk.Widgets.Music.Style
{
    public class Style4 : StyleBase
    {
        private Line[] _radialBars = new Line[0];
        private DispatcherTimer _renderTimer;
        private DispatcherTimer _infoTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 48);
            if (barsCount < 24) barsCount = 24;
            if (barsCount > 64) barsCount = 64;

            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var showTrackInfo = GetBool(instance, "show_track_info", true);
            var showLabel = GetBool(instance, "show_label", false);

            var trackService = TrackInfoService.Instance;

            var root = new Grid();

            var bg = BuildBackground();
            root.Children.Add(bg);

            var content = new Grid { Margin = new Thickness(16) };
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(content);

            // ═══ Canvas for Circle ═══
            var canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var accentColor = ResolveAccentColor(instance);
            var accentBrush = new SolidColorBrush(accentColor);

            // ═══ Layout constants ═══
            const double CANVAS_SIZE = 160;
            const double CENTER_X = CANVAS_SIZE / 2;
            const double CENTER_Y = CANVAS_SIZE / 2;
            const double INNER_RADIUS = 45;
            const double BASE_BAR_LENGTH = 8;
            const double MAX_BAR_LENGTH = 25;

            canvas.Width = CANVAS_SIZE;
            canvas.Height = CANVAS_SIZE;

            // ═══ Inner circle (album art placeholder) ═══
            var innerCircle = new Ellipse
            {
                Width = INNER_RADIUS * 2,
                Height = INNER_RADIUS * 2,
                Fill = new SolidColorBrush(Color.FromArgb(0x20, accentColor.R, accentColor.G, accentColor.B)),
                Stroke = accentBrush,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(innerCircle, CENTER_X - INNER_RADIUS);
            Canvas.SetTop(innerCircle, CENTER_Y - INNER_RADIUS);
            canvas.Children.Add(innerCircle);

            // ═══ Cover art inside circle ═══
            if (trackService.CoverArt != null)
            {
                var clip = new EllipseGeometry(
                    new Point(INNER_RADIUS, INNER_RADIUS),
                    INNER_RADIUS - 2,
                    INNER_RADIUS - 2);

                var coverImage = new Image
                {
                    Source = trackService.CoverArt,
                    Width = INNER_RADIUS * 2 - 4,
                    Height = INNER_RADIUS * 2 - 4,
                    Stretch = Stretch.UniformToFill,
                    Clip = clip
                };
                Canvas.SetLeft(coverImage, CENTER_X - INNER_RADIUS + 2);
                Canvas.SetTop(coverImage, CENTER_Y - INNER_RADIUS + 2);
                canvas.Children.Add(coverImage);
            }

            // ═══ Radial bars ═══
            _radialBars = new Line[barsCount];

            for (int i = 0; i < barsCount; i++)
            {
                double angle = (double)i / barsCount * 360 - 90; // start from top
                double rad = angle * Math.PI / 180;

                var bar = new Line
                {
                    X1 = CENTER_X + Math.Cos(rad) * INNER_RADIUS,
                    Y1 = CENTER_Y + Math.Sin(rad) * INNER_RADIUS,
                    X2 = CENTER_X + Math.Cos(rad) * (INNER_RADIUS + BASE_BAR_LENGTH),
                    Y2 = CENTER_Y + Math.Sin(rad) * (INNER_RADIUS + BASE_BAR_LENGTH),
                    Stroke = accentBrush,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                _radialBars[i] = bar;
                canvas.Children.Add(bar);
            }

            Grid.SetRow(canvas, 0);
            content.Children.Add(canvas);

            // ═══ Track Info ═══
            var infoPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = showTrackInfo ? Visibility.Visible : Visibility.Collapsed
            };

            var titleText = new TextBlock
            {
                Text = trackService.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(GetThemeColor("WidgetTextPrimary", Colors.White)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };
            infoPanel.Children.Add(titleText);

            var artistText = new TextBlock
            {
                Text = trackService.Artist,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(0xA0,
                    GetThemeColor("WidgetTextPrimary", Colors.White).R,
                    GetThemeColor("WidgetTextPrimary", Colors.White).G,
                    GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };
            infoPanel.Children.Add(artistText);

            if (showLabel)
            {
                var label = new TextBlock
                {
                    Text = "NOW PLAYING",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x80,
                        GetThemeColor("WidgetTextPrimary", Colors.White).R,
                        GetThemeColor("WidgetTextPrimary", Colors.White).G,
                        GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                infoPanel.Children.Add(label);
            }

            Grid.SetRow(infoPanel, 1);
            content.Children.Add(infoPanel);

            // ═══ Render Loop ═══
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = AudioCaptureService.Instance.Spectrum;
                var spectrumLen = spectrum.Length;

                // Accent dynamic
                var newAccentColor = ResolveAccentColor(instance);
                if (accentBrush.Color != newAccentColor)
                {
                    accentBrush.Color = newAccentColor;
                    innerCircle.Stroke = accentBrush;
                }

                for (int i = 0; i < _radialBars.Length; i++)
                {
                    // Sample spectrum
                    var srcIdx = (int)(i / (double)_radialBars.Length * spectrumLen);
                    srcIdx = Math.Clamp(srcIdx, 0, spectrumLen - 1);

                    var amplitude = spectrum[srcIdx] * sensitivity;
                    amplitude = Math.Clamp(amplitude, 0, 1);

                    var barLength = BASE_BAR_LENGTH + amplitude * (MAX_BAR_LENGTH - BASE_BAR_LENGTH);

                    // Update line endpoint
                    double angle = (double)i / _radialBars.Length * 360 - 90;
                    double rad = angle * Math.PI / 180;

                    _radialBars[i].X2 = CENTER_X + Math.Cos(rad) * (INNER_RADIUS + barLength);
                    _radialBars[i].Y2 = CENTER_Y + Math.Sin(rad) * (INNER_RADIUS + barLength);
                }
            };
            _renderTimer.Start();

            // ═══ Track info update ═══
            _infoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _infoTimer.Tick += (s, e) =>
            {
                titleText.Text = trackService.Title;
                artistText.Text = trackService.Artist;
            };
            _infoTimer.Start();

            root.Unloaded += (s, e) =>
            {
                _renderTimer?.Stop();
                _renderTimer = null;
                _infoTimer?.Stop();
                _infoTimer = null;
            };

            return root;
        }
    }
}