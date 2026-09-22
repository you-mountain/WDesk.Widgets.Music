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
        private DispatcherTimer _infoTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 32);
            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var showLabel = GetBool(instance, "show_label", true);
            var showTrackInfo = GetBool(instance, "show_track_info", true);
            var showCover = GetBool(instance, "show_cover", true);

            var trackService = TrackInfoService.Instance;

            var root = new Grid();

            // ═══ Background — WDesk core ═══
            var bg = BuildBackground();
            root.Children.Add(bg);

            // ═══ Layout ═══
            var content = new Grid { Margin = new Thickness(16, 14, 16, 14) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Track info
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Label
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Bars
            root.Children.Add(content);

            // ═══ Track Info Row ═══
            var trackInfoPanel = new Grid();
            trackInfoPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // Cover
            trackInfoPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
            trackInfoPanel.Visibility = showTrackInfo ? Visibility.Visible : Visibility.Collapsed;

            // Cover
            var coverBox = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x3B, 0x82, 0xF6))
            };

            if (trackService.CoverArt != null)
            {
                coverBox.Child = new Image
                {
                    Source = trackService.CoverArt,
                    Stretch = Stretch.UniformToFill
                };
            }
            coverBox.Visibility = showCover ? Visibility.Visible : Visibility.Collapsed;

            Grid.SetColumn(coverBox, 0);
            trackInfoPanel.Children.Add(coverBox);

            // Text
            var textPanel = new StackPanel
            {
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleText = new TextBlock
            {
                Text = trackService.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(GetThemeColor("WidgetTextPrimary", Colors.White)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(titleText);

            var artistText = new TextBlock
            {
                Text = trackService.Artist,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0xA0,
                    GetThemeColor("WidgetTextPrimary", Colors.White).R,
                    GetThemeColor("WidgetTextPrimary", Colors.White).G,
                    GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(artistText);

            Grid.SetColumn(textPanel, 1);
            trackInfoPanel.Children.Add(textPanel);

            Grid.SetRow(trackInfoPanel, 0);
            content.Children.Add(trackInfoPanel);

            // ═══ Label ═══
            if (showLabel)
            {
                var label = new TextBlock
                {
                    Text = "NOW PLAYING",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xA0,
                        GetThemeColor("WidgetTextPrimary", Colors.White).R,
                        GetThemeColor("WidgetTextPrimary", Colors.White).G,
                        GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                Grid.SetRow(label, 1);
                content.Children.Add(label);
            }

            // ═══ Bars ═══
            var accentColor = ResolveAccentColor(instance);
            var accentBrush = new SolidColorBrush(accentColor);

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

            Grid.SetRow(eqGrid, 2);
            content.Children.Add(eqGrid);

            // ═══ Render Loop ═══
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = AudioCaptureService.Instance.Spectrum;
                var spectrumLen = spectrum.Length;
                var maxHeight = 60.0;

                // ★ Accent از cover
                var newAccentColor = ResolveAccentColor(instance);
                if (accentBrush.Color != newAccentColor)
                {
                    accentBrush.Color = newAccentColor;
                }

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

            // ═══ Track Info Update ═══
            _infoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _infoTimer.Tick += (s, e) =>
            {
                titleText.Text = trackService.Title;
                artistText.Text = trackService.Artist;

                if (showCover && trackService.CoverArt != null && coverBox.Child is not Image)
                {
                    coverBox.Child = new Image
                    {
                        Source = trackService.CoverArt,
                        Stretch = Stretch.UniformToFill
                    };
                }
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