using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WDesk.Widgets.Music;

/// <summary>
/// ★ Track Info — Event-based، بدون polling.
/// از Windows SMTC استفاده می‌کنه (Spotify، YouTube Music، VLC، ...).
/// </summary>
public class TrackInfoService
{
    private static TrackInfoService? _instance;
    private static readonly object _initLock = new();

    public static TrackInfoService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_initLock)
                {
                    _instance ??= new TrackInfoService();
                }
            }
            return _instance;
        }
    }

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public string Title { get; private set; } = "No Track";
    public string Artist { get; private set; } = "Unknown Artist";
    public string Album { get; private set; } = "";
    public ImageSource? CoverArt { get; private set; }
    public Color DominantColor { get; private set; } = Color.FromRgb(0x3B, 0x82, 0xF6);
    public bool IsPlaying { get; private set; }

    public event Action? TrackChanged;
    public event Action? PlaybackStateChanged;

    private TrackInfoService()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            if (_sessionManager == null) return;

            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
            _sessionManager.SessionsChanged += OnSessionsChanged;

            OnCurrentSessionChanged(_sessionManager, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrackInfo] Init failed: {ex.Message}");
        }
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        OnCurrentSessionChanged(sender, null);
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs? args)
    {
        try
        {
            var session = sender.GetCurrentSession();

            if (session == null)
            {
                _currentSession = null;
                Title = "No Track";
                Artist = "Unknown Artist";
                CoverArt = null;
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
            System.Diagnostics.Debug.WriteLine($"[TrackInfo] Session: {ex.Message}");
        }
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateTrackInfoAsync();
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        try
        {
            var info = sender.GetPlaybackInfo();
            if (info == null) return;

            var newPlaying = info.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            if (newPlaying != IsPlaying)
            {
                IsPlaying = newPlaying;
                PlaybackStateChanged?.Invoke();
            }
        }
        catch { }
    }

    public async Task UpdateTrackInfoAsync()
    {
        try
        {
            _currentSession ??= _sessionManager?.GetCurrentSession();

            if (_currentSession == null)
            {
                Title = "No Track";
                Artist = "Unknown Artist";
                IsPlaying = false;
                return;
            }

            var props = await _currentSession.TryGetMediaPropertiesAsync();
            if (props == null) return;

            var newTitle = string.IsNullOrEmpty(props.Title) ? "No Track" : props.Title;
            var newArtist = string.IsNullOrEmpty(props.Artist) ? "Unknown Artist" : props.Artist;
            var newAlbum = props.AlbumTitle ?? "";

            var playbackInfo = _currentSession.GetPlaybackInfo();
            var newPlaying = playbackInfo?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            ImageSource? newCover = null;
            if (props.Thumbnail != null)
                newCover = await GetCoverArtAsync(props.Thumbnail);

            var trackChanged = (Title != newTitle) || (Artist != newArtist);
            var coverChanged = !ReferenceEquals(CoverArt, newCover);

            Title = newTitle;
            Artist = newArtist;
            Album = newAlbum;
            CoverArt = newCover;
            IsPlaying = newPlaying;

            if (newCover != null)
                DominantColor = ExtractDominantColor(newCover);

            if (trackChanged || coverChanged)
                TrackChanged?.Invoke();

            PlaybackStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrackInfo] Update: {ex.Message}");
        }
    }

    private static async Task<ImageSource?> GetCoverArtAsync(IRandomAccessStreamReference thumb)
    {
        try
        {
            using var stream = await thumb.OpenReadAsync();
            if (stream == null) return null;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream.AsStreamForRead();
            bmp.EndInit();
            bmp.Freeze();

            return bmp;
        }
        catch { return null; }
    }

    private static Color ExtractDominantColor(ImageSource image)
    {
        try
        {
            if (image is not BitmapSource bmp) return Color.FromRgb(0x3B, 0x82, 0xF6);

            var resized = new TransformedBitmap(bmp,
                new ScaleTransform(16.0 / bmp.PixelWidth, 16.0 / bmp.PixelHeight));

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

                int brightness = (r + g + b) / 3;
                if (brightness < 30 || brightness > 220) continue;

                rSum += r; gSum += g; bSum += b;
                count++;
            }

            if (count == 0) return Color.FromRgb(0x3B, 0x82, 0xF6);

            byte avgR = (byte)(rSum / count);
            byte avgG = (byte)(gSum / count);
            byte avgB = (byte)(bSum / count);

            return BoostSaturation(Color.FromRgb(avgR, avgG, avgB));
        }
        catch { return Color.FromRgb(0x3B, 0x82, 0xF6); }
    }

    private static Color BoostSaturation(Color color)
    {
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

        s = Math.Max(s, 0.7);
        l = Math.Max(Math.Min(l, 0.6), 0.4);

        return HslToRgb(h, s, l);
    }

    private static Color HslToRgb(double h, double s, double l)
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

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}