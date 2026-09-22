using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace WDesk.Widgets.Music
{
    /// <summary>
    /// ★ Track Info Service — از Windows SMTC می‌خونه
    /// 
    /// با Spotify، YouTube Music، VLC، Windows Media Player، ... کار می‌کنه
    /// </summary>
    public class TrackInfoService
    {
        private static TrackInfoService _instance;
        private static readonly object _lock = new object();

        public static TrackInfoService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new TrackInfoService();
                    }
                }
                return _instance;
            }
        }

        private GlobalSystemMediaTransportControlsSessionManager _sessionManager;
        private GlobalSystemMediaTransportControlsSession _currentSession;

        // ★ Current Track Info
        public string Title { get; private set; } = "No Track";
        public string Artist { get; private set; } = "Unknown Artist";
        public string Album { get; private set; } = "";
        public ImageSource CoverArt { get; private set; }
        public Color DominantColor { get; private set; } = Color.FromRgb(0x3B, 0x82, 0xF6);
        public bool IsPlaying { get; private set; }

        public event Action TrackChanged;
        public event Action PlaybackStateChanged;

        private DateTime _lastUpdate = DateTime.MinValue;
        private const int UPDATE_INTERVAL_MS = 500;

        private TrackInfoService()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnSessionChanged;
                    _sessionManager.SessionsChanged += OnSessionsChanged;
                    OnSessionChanged(_sessionManager, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackInfo] Init failed: {ex.Message}");
            }
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            OnSessionChanged(sender, null);
        }

        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            try
            {
                var session = sender.GetCurrentSession();
                if (session == null)
                {
                    _currentSession = null;
                    Title = "No Track";
                    Artist = "Unknown Artist";
                    IsPlaying = false;
                    TrackChanged?.Invoke();
                    return;
                }

                _currentSession = session;
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;

                _ = UpdateTrackInfoAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackInfo] Session change: {ex.Message}");
            }
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = UpdateTrackInfoAsync();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                var playbackInfo = sender.GetPlaybackInfo();
                if (playbackInfo != null)
                {
                    var newIsPlaying = playbackInfo.PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    if (newIsPlaying != IsPlaying)
                    {
                        IsPlaying = newIsPlaying;
                        PlaybackStateChanged?.Invoke();
                    }
                }
            }
            catch { }
        }

        public async Task UpdateTrackInfoAsync()
        {
            try
            {
                if (_currentSession == null)
                {
                    if (_sessionManager != null)
                    {
                        _currentSession = _sessionManager.GetCurrentSession();
                    }

                    if (_currentSession == null)
                    {
                        Title = "No Track";
                        Artist = "Unknown Artist";
                        IsPlaying = false;
                        return;
                    }
                }

                var mediaProps = await _currentSession.TryGetMediaPropertiesAsync();
                if (mediaProps == null) return;

                // ★ Title
                var newTitle = string.IsNullOrEmpty(mediaProps.Title)
                    ? "No Track"
                    : mediaProps.Title;

                // ★ Artist
                var newArtist = string.IsNullOrEmpty(mediaProps.Artist)
                    ? "Unknown Artist"
                    : mediaProps.Artist;

                // ★ Album
                var newAlbum = mediaProps.AlbumTitle ?? "";

                // ★ Playback status
                var playbackInfo = _currentSession.GetPlaybackInfo();
                var newIsPlaying = playbackInfo?.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                // ★ Cover Art
                ImageSource newCover = null;
                if (mediaProps.Thumbnail != null)
                {
                    newCover = await GetCoverArtAsync(mediaProps.Thumbnail);
                }

                // ★ Detect changes
                var trackChanged = (Title != newTitle) || (Artist != newArtist);

                Title = newTitle;
                Artist = newArtist;
                Album = newAlbum;
                CoverArt = newCover;
                IsPlaying = newIsPlaying;

                // ★ Extract dominant color
                if (newCover != null)
                {
                    DominantColor = ExtractDominantColor(newCover);
                }

                if (trackChanged)
                {
                    TrackChanged?.Invoke();
                }

                PlaybackStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrackInfo] Update: {ex.Message}");
            }
        }

        private async Task<ImageSource> GetCoverArtAsync(IRandomAccessStreamReference thumbnail)
        {
            try
            {
                using var stream = await thumbnail.OpenReadAsync();
                if (stream == null) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream.AsStreamForRead();
                bmp.EndInit();
                bmp.Freeze();

                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Color ExtractDominantColor(ImageSource image)
        {
            try
            {
                var bmp = image as BitmapSource;
                if (bmp == null) return Color.FromRgb(0x3B, 0x82, 0xF6);

                // ★ Resize to 16x16 for fast color analysis
                var resized = new TransformedBitmap(bmp, new System.Windows.Media.ScaleTransform(
                    16.0 / bmp.PixelWidth,
                    16.0 / bmp.PixelHeight));

                var converted = new FormatConvertedBitmap(resized, PixelFormats.Bgra32, null, 0);

                int width = converted.PixelWidth;
                int height = converted.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                converted.CopyPixels(pixels, stride, 0);

                long rSum = 0, gSum = 0, bSum = 0;
                int count = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a < 128) continue;

                    // ★ Skip too dark/light pixels
                    int brightness = (r + g + b) / 3;
                    if (brightness < 30 || brightness > 220) continue;

                    rSum += r;
                    gSum += g;
                    bSum += b;
                    count++;
                }

                if (count == 0) return Color.FromRgb(0x3B, 0x82, 0xF6);

                byte avgR = (byte)(rSum / count);
                byte avgG = (byte)(gSum / count);
                byte avgB = (byte)(bSum / count);

                // ★ Boost saturation
                var color = Color.FromRgb(avgR, avgG, avgB);
                return BoostSaturation(color);
            }
            catch
            {
                return Color.FromRgb(0x3B, 0x82, 0xF6);
            }
        }

        private Color BoostSaturation(Color color)
        {
            // Convert to HSL
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2;
            double h = 0, s = 0;

            if (max != min)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;

                h /= 6;
            }

            // ★ Boost saturation to 0.7
            s = Math.Max(s, 0.7);
            l = Math.Max(Math.Min(l, 0.6), 0.4);

            // HSL to RGB
            return HslToRgb(h, s, l);
        }

        private Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;

                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromRgb(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255));
        }

        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}