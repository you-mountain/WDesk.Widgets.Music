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
        private Rectangle[] _bars = Array.Empty<Rectangle>();
        private DispatcherTimer? _renderTimer;
        private TrackInfoService? _track;

        // Track UI refs
        private Border? _coverBox;
        private TextBlock? _titleText;
        private TextBlock? _artistText;
        private SolidColorBrush? _accentBrush;
        private Border? _innerCircle;

        public override FrameworkElement Build(PlacedWidget instance)
        {
            var barsCount = GetInt(instance, "bars_count", 32);
            var sensitivity = GetInt(instance, "sensitivity", 100) / 100.0;
            var showLabel = GetBool(instance, "show_label", true);
            var showTrackInfo = GetBool(instance, "show_track_info", true);
            var showCover = GetBool(instance, "show_cover", true);

            _track = TrackInfoService.Instance;

            var root = new Grid();
            root.Children.Add(BuildBackground());

            var content = new Grid { Margin = new Thickness(16, 14, 16, 14) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(content);

            // ═══ Track Info ═══
            var trackPanel = new Grid
            {
                Visibility = showTrackInfo ? Visibility.Visible : Visibility.Collapsed
            };
            trackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            trackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _coverBox = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x3B, 0x82, 0xF6)),
                Visibility = showCover ? Visibility.Visible : Visibility.Collapsed
            };

            if (_track.CoverArt != null)
                _coverBox.Child = new Image { Source = _track.CoverArt, Stretch = Stretch.UniformToFill };

            Grid.SetColumn(_coverBox, 0);
            trackPanel.Children.Add(_coverBox);

            var textStack = new StackPanel
            {
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _titleText = new TextBlock
            {
                Text = _track.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(GetThemeColor("WidgetTextPrimary", Colors.White)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(_titleText);

            _artistText = new TextBlock
            {
                Text = _track.Artist,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0xA0,
                    GetThemeColor("WidgetTextPrimary", Colors.White).R,
                    GetThemeColor("WidgetTextPrimary", Colors.White).G,
                    GetThemeColor("WidgetTextPrimary", Colors.White).B)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(_artistText);

            Grid.SetColumn(textStack, 1);
            trackPanel.Children.Add(textStack);

            Grid.SetRow(trackPanel, 0);
            content.Children.Add(trackPanel);

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
            _accentBrush = new SolidColorBrush(accentColor);

            var eqGrid = new UniformGrid { Rows = 1, VerticalAlignment = VerticalAlignment.Bottom };
            _bars = new Rectangle[barsCount];

            for (int i = 0; i < barsCount; i++)
            {
                var bar = new Rectangle
                {
                    Height = 4,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = _accentBrush,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(1, 0, 1, 0),
                    MinHeight = 4
                };
                _bars[i] = bar;
                eqGrid.Children.Add(bar);
            }

            Grid.SetRow(eqGrid, 2);
            content.Children.Add(eqGrid);

            // ═══ Event subscriptions ═══
            void onTrackChanged()
            {
                if (root.Dispatcher.CheckAccess())
                    UpdateTrackInfo(showCover);
                else
                    root.Dispatcher.BeginInvoke(new Action(() => UpdateTrackInfo(showCover)));
            }

            _track.TrackChanged += onTrackChanged;

            // ═══ Render timer ═══
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _renderTimer.Tick += (s, e) => RenderBars(sensitivity);
            _renderTimer.Start();

            // ═══ Cleanup ═══
            root.Unloaded += (s, e) =>
            {
                _renderTimer?.Stop();
                _renderTimer = null;

                if (_track != null)
                    _track.TrackChanged -= onTrackChanged;

                _track = null;
            };

            return root;
        }

        private void RenderBars(double sensitivity)
        {
            try
            {
                var spectrum = AudioCaptureService.Instance.Spectrum;
                var len = spectrum.Length;
                if (len == 0) return;

                const double maxHeight = 60.0;

                // Dynamic accent from cover art
                if (_accentBrush != null && _track != null)
                {
                    var newColor = _track.DominantColor;
                    if (_accentBrush.Color != newColor)
                        _accentBrush.Color = newColor;
                }

                for (int i = 0; i < _bars.Length; i++)
                {
                    var srcIdx = (int)(i / (double)_bars.Length * len);
                    srcIdx = Math.Clamp(srcIdx, 0, len - 1);

                    var amp = Math.Clamp(spectrum[srcIdx] * sensitivity, 0, 1);
                    var target = 4 + amp * (maxHeight - 4);
                    var current = _bars[i].Height;
                    var smoothed = Math.Max(target, current * 0.7);

                    _bars[i].Height = Math.Min(smoothed, maxHeight);
                }
            }
            catch { }
        }

        private void UpdateTrackInfo(bool showCover)
        {
            try
            {
                if (_track == null) return;

                if (_titleText != null) _titleText.Text = _track.Title;
                if (_artistText != null) _artistText.Text = _track.Artist;

                if (showCover && _track.CoverArt != null && _coverBox != null)
                {
                    _coverBox.Child = new Image
                    {
                        Source = _track.CoverArt,
                        Stretch = Stretch.UniformToFill
                    };
                }
            }
            catch { }
        }
    }
}