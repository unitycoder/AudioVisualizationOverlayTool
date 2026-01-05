using NAudio.Wave;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AudioFXOverlay
{

    public partial class MainWindow : Window
    {
        private readonly SystemAudioLoopback _audio = new SystemAudioLoopback();
        private readonly DispatcherTimer _timer;

        private FloatRingBuffer _ring = new FloatRingBuffer(1);
        private float[] _snapshot = Array.Empty<float>();
        private readonly PointCollection _points = new PointCollection();
        private int _maxSamples;

        public MainWindow()
        {
            InitializeComponent();

            WaveLine.Points = _points;

            _audio.SamplesAvailable += samples =>
            {
                _ring.Write(samples);
            };

            _audio.Start();

            int channels = Math.Max(1, _audio.Channels);
            int sampleRate = Math.Max(1, _audio.SampleRate);

            _maxSamples = Math.Max(1, (sampleRate / 10) * channels);
            _ring = new FloatRingBuffer(_maxSamples * 2); // small headroom
            _snapshot = new float[_maxSamples];

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 fps
            _timer.Tick += (_, __) => RenderWaveform();
            _timer.Start();

            Closed += (_, __) =>
            {
                _timer.Stop();
                _audio.Dispose();
            };
        }

        private void RenderWaveform()
        {
            int available = _ring.Count;
            if (available <= 0)
                return;

            int sampleCount = Math.Min(_maxSamples, available);
            if (sampleCount <= 0)
                return;

            if (_snapshot.Length < sampleCount)
                _snapshot = new float[sampleCount];

            _ring.ReadLatest(_snapshot.AsSpan(0, sampleCount), sampleCount);

            double w = Math.Max(1, WaveCanvas.ActualWidth);
            double h = Math.Max(1, WaveCanvas.ActualHeight);

            int channels = Math.Max(1, _audio.Channels);
            int frames = sampleCount / channels;
            if (frames <= 0)
            {
                // Startup safety: if we don't yet have a full interleaved frame, treat as mono.
                channels = 1;
                frames = sampleCount;
                if (frames <= 0)
                    return;
            }

            int targetPoints = (int)Math.Min(w, 2000);
            targetPoints = Math.Max(32, targetPoints);

            int step = Math.Max(1, frames / targetPoints);

            _points.Clear();

            int frameIndex = 0;
            for (int i = 0; i < targetPoints; i++)
            {
                int start = frameIndex;
                int end = Math.Min(frames, start + step);
                if (start >= end) break;

                float max = -1f;

                for (int f = start; f < end; f++)
                {
                    float v;
                    int baseIndex = f * channels;

                    if (channels == 2 && baseIndex + 1 < sampleCount)
                    {
                        v = (_snapshot[baseIndex] + _snapshot[baseIndex + 1]) * 0.5f;
                    }
                    else
                    {
                        float sum = 0f;
                        int limit = Math.Min(channels, sampleCount - baseIndex);
                        for (int c = 0; c < limit; c++)
                            sum += _snapshot[baseIndex + c];
                        v = sum / Math.Max(1, limit);
                    }

                    if (v > max) max = v;
                }

                double x = (i / (double)(targetPoints - 1)) * (w - 1);
                double y = (0.5 - (max * 0.45)) * (h - 1);
                _points.Add(new Point(x, y));

                frameIndex += step;
            }
        }

        private static int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return Math.Max(1, v);
        }


    } // class MainWindow
} // namespace AudioFXOverlay