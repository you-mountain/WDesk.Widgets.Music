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
        private DispatcherTimer _infoTimer;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 16);
            if (barsCount > 32) barsCount = 32;

            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var showTrackInfo = GetBool(instance, "show_track_info", true);
            var showCover = GetBool(instance, "show_cover", true);

            var trackService = TrackInfoService.Instance;
            var accentColor = ResolveAccentColor(instance);
            var accentBrush = new SolidColorBrush(accentColor);

            var root = new Grid();

            var bg = BuildBackground();
            root.Children.Add(bg);

            var content = new Grid { Margin = new Thickness(12, 8, 12, 8) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(content);

            // ═══ Cover ═══
            var coverBox = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(0x30, accentColor.R, accentColor.G, accentColor.B))
            };

            if (trackService?.CoverArt != null && showCover)
            {
                coverBox.Child = new Image
                {
                    Source = trackService.CoverArt,
                    Stretch = Stretch.UniformToFill
                };
            }
            else
            {
                coverBox.Child = new TextBlock
                {
                    Text = "\uE8D6",
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = accentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            Grid.SetColumn(coverBox, 0);
            content.Children.Add(coverBox);

            // ═══ Info + Mini EQ ═══
            var rightGrid = new Grid { Margin = new Thickness(12, 0, 0, 0) };
            rightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = showTrackInfo ? Visibility.Visible : Visibility.Collapsed
            };

            var title = new TextBlock
            {
                Text = trackService?.Title ?? "No Track",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(GetThemeColor("WidgetTextPrimary", Colors.White)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            info.Children.Add(title);

            var artist = new TextBlock
            {
                Text = trackService?.Artist ?? "Unknown Artist",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0xA0,
                    GetThemeColor("WidgetTextPrimary", Colors.White).R,
                    GetThemeColor("WidgetTextPrimary", Colors.White).G,
                    GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            info.Children.Add(artist);

            Grid.SetColumn(info, 0);
            rightGrid.Children.Add(info);

            // ═══ Mini Bars ═══
            var eqGrid = new UniformGrid
            {
                Rows = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 50,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0)
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
            rightGrid.Children.Add(eqGrid);

            Grid.SetColumn(rightGrid, 1);
            content.Children.Add(rightGrid);

            // ═══ Render Loop ═══
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _renderTimer.Tick += (s, e) =>
            {
                var spectrum = AudioCaptureService.Instance.Spectrum;
                var spectrumLen = spectrum.Length;
                var maxHeight = 28.0;

                // Update accent
                var newAccentColor = ResolveAccentColor(instance);
                if (accentBrush.Color != newAccentColor)
                {
                    accentBrush.Color = newAccentColor;
                }

                for (int i = 0; i < _bars.Length; i++)
                {
                    var srcIdx = (int)(i / (double)_bars.Length * spectrumLen);
                    srcIdx = Math.Clamp(srcIdx, 0, spectrumLen - 1);

                    var amplitude = spectrum[srcIdx] * sensitivity;
                    amplitude = Math.Clamp(amplitude, 0, 1);

                    var barHeight = 3 + amplitude * (maxHeight - 3);
                    var currentHeight = _bars[i].Height;
                    var newHeight = Math.Max(barHeight, currentHeight * 0.75);

                    _bars[i].Height = Math.Min(newHeight, maxHeight);
                }
            };
            _renderTimer.Start();

            // ═══ Track info update ═══
            _infoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _infoTimer.Tick += (s, e) =>
            {
                if (trackService == null) return;
                title.Text = trackService.Title;
                artist.Text = trackService.Artist;

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