using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Numerics;
using Complex = NAudio.Dsp.Complex;
using MediaColor = System.Windows.Media.Color;

namespace WDesk.Widgets.Music
{
    public class AudioCaptureService : IDisposable
    {
        private static AudioCaptureService _instance;
        private static readonly object _lock = new object();

        public static AudioCaptureService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new AudioCaptureService();
                    }
                }
                return _instance;
            }
        }

        private const int FFT_SIZE = 2048;
        private const int FFT_LOG_SIZE = 11;
        private const int SPECTRUM_BARS = 64;
        private const int FALLBACK_SAMPLE_RATE = 44100;
        private const double MIN_FREQ = 20;
        private const double MAX_FREQ = 16000;

        private WasapiLoopbackCapture _capture;
        private readonly Complex[] _fftBuffer = new Complex[FFT_SIZE];
        private int _fftBufferPos = 0;
        private readonly float[] _window = new float[FFT_SIZE];
        private readonly float[] _spectrum = new float[SPECTRUM_BARS];
        private readonly float[] _smoothedSpectrum = new float[SPECTRUM_BARS];

        private const float SMOOTHING = 0.55f;
        private float _currentRms = 0f;

        public bool IsCapturing { get { return _capture != null; } }
        public float[] Spectrum { get { return _spectrum; } }

        public MediaColor DominantColor => TrackInfoService.Instance.DominantColor;
        public float CurrentRms { get { return _currentRms; } }
        public bool IsSilent { get { return _currentRms < 0.003f; } }

        private AudioCaptureService()
        {
            for (int i = 0; i < FFT_SIZE; i++)
            {
                _window[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FFT_SIZE - 1))));
            }
        }

        public void Start()
        {
            if (_capture != null) return;

            try
            {
                AssemblyResolver.EnsureInitialized();

                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AudioCapture] Start: " + ex.Message);
                _capture = null;
            }
        }

        public void Stop()
        {
            if (_capture == null) return;

            try
            {
                _capture.StopRecording();
                _capture.Dispose();
            }
            catch { }

            _capture = null;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || _capture == null) return;

            try
            {
                var format = _capture.WaveFormat;
                int bytesPerSample = format.BitsPerSample / 8;
                if (bytesPerSample == 0) bytesPerSample = 4;

                int channels = format.Channels;
                if (channels <= 0) channels = 1;

                int frames = e.BytesRecorded / (bytesPerSample * channels);
                float sumSquares = 0;

                for (int frame = 0; frame < frames; frame++)
                {
                    float sample = 0;

                    if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        int offset = frame * channels * 4;
                        if (offset + 4 > e.BytesRecorded) break;
                        sample = BitConverter.ToSingle(e.Buffer, offset);

                        if (channels >= 2)
                        {
                            int ch2 = offset + 4;
                            if (ch2 + 4 <= e.BytesRecorded)
                                sample = (sample + BitConverter.ToSingle(e.Buffer, ch2)) / 2f;
                        }
                    }
                    else if (format.BitsPerSample == 16)
                    {
                        int offset = frame * channels * 2;
                        if (offset + 2 > e.BytesRecorded) break;
                        short s = BitConverter.ToInt16(e.Buffer, offset);
                        sample = s / 32768f;

                        if (channels >= 2)
                        {
                            int ch2 = offset + 2;
                            if (ch2 + 2 <= e.BytesRecorded)
                                sample = (sample + BitConverter.ToInt16(e.Buffer, ch2) / 32768f) / 2f;
                        }
                    }

                    sumSquares += sample * sample;
                    AddSampleToFFT(sample);
                }

                if (frames > 0)
                {
                    float rms = (float)Math.Sqrt(sumSquares / frames);
                    _currentRms = _currentRms * 0.8f + rms * 0.2f;
                }
            }
            catch { }
        }

        private void AddSampleToFFT(float sample)
        {
            _fftBuffer[_fftBufferPos].X = sample * _window[_fftBufferPos];
            _fftBuffer[_fftBufferPos].Y = 0;

            _fftBufferPos++;

            if (_fftBufferPos >= FFT_SIZE)
            {
                _fftBufferPos = 0;
                ProcessFFT();
            }
        }

        private void ProcessFFT()
        {
            try
            {
                var fft = new Complex[FFT_SIZE];
                Array.Copy(_fftBuffer, fft, FFT_SIZE);

                FastFourierTransform.FFT(true, FFT_LOG_SIZE, fft);

                var sampleRate = _capture != null ? _capture.WaveFormat.SampleRate : FALLBACK_SAMPLE_RATE;
                var binCount = FFT_SIZE / 2;

                double logMin = Math.Log10(MIN_FREQ);
                double logMax = Math.Log10(MAX_FREQ);

                for (int bar = 0; bar < SPECTRUM_BARS; bar++)
                {
                    double t1 = (double)bar / SPECTRUM_BARS;
                    double t2 = (double)(bar + 1) / SPECTRUM_BARS;

                    double f1 = Math.Pow(10, logMin + (logMax - logMin) * t1);
                    double f2 = Math.Pow(10, logMin + (logMax - logMin) * t2);

                    int bin1 = (int)(f1 / sampleRate * FFT_SIZE);
                    int bin2 = (int)(f2 / sampleRate * FFT_SIZE);

                    if (bin1 < 0) bin1 = 0;
                    if (bin1 >= binCount) bin1 = binCount - 1;
                    if (bin2 <= bin1) bin2 = bin1 + 1;
                    if (bin2 > binCount) bin2 = binCount;

                    float sum = 0;
                    for (int bin = bin1; bin < bin2; bin++)
                    {
                        float real = fft[bin].X;
                        float imag = fft[bin].Y;
                        float mag = (float)Math.Sqrt(real * real + imag * imag);
                        sum += mag;
                    }
                    float avg = sum / (bin2 - bin1);

                    float db = (float)(20 * Math.Log10(avg + 1e-9f));
                    float normalized = (db + 85f) / 85f;
                    if (normalized < 0) normalized = 0;
                    if (normalized > 1) normalized = 1;

                    double freqMid = (f1 + f2) / 2;
                    if (freqMid < 250) normalized *= 1.4f;
                    else if (freqMid > 8000) normalized *= 0.85f;
                    if (normalized < 0) normalized = 0;
                    if (normalized > 1) normalized = 1;

                    _spectrum[bar] = normalized;
                }

                for (int i = 0; i < SPECTRUM_BARS; i++)
                {
                    _smoothedSpectrum[i] = _smoothedSpectrum[i] * SMOOTHING + _spectrum[i] * (1 - SMOOTHING);
                    _spectrum[i] = _smoothedSpectrum[i];
                }
            }
            catch { }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            _capture = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}