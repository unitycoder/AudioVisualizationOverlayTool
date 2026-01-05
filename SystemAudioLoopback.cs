using System;
using NAudio.Wave;

public sealed class SystemAudioLoopback : IDisposable
{
    private WasapiLoopbackCapture _capture;

    // Interleaved float samples, range roughly -1..1 (after conversion)
    public event Action<float[]> SamplesAvailable;

    public int SampleRate => _capture?.WaveFormat.SampleRate ?? 0;
    public int Channels => _capture?.WaveFormat.Channels ?? 0;

    public void Start()
    {
        _capture = new WasapiLoopbackCapture(); // default render device
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, __) => { };
        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture?.StopRecording();
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        // WasapiLoopbackCapture typically outputs 32-bit float, but don't assume.
        // Convert bytes -> floats in a safe way.
        var wf = _capture.WaveFormat;

        float[] samples = wf.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat when wf.BitsPerSample == 32 => BytesToFloat32(e.Buffer, e.BytesRecorded),
            WaveFormatEncoding.Pcm when wf.BitsPerSample == 16 => Pcm16ToFloat(e.Buffer, e.BytesRecorded),
            WaveFormatEncoding.Pcm when wf.BitsPerSample == 24 => Pcm24ToFloat(e.Buffer, e.BytesRecorded),
            WaveFormatEncoding.Pcm when wf.BitsPerSample == 32 => Pcm32ToFloat(e.Buffer, e.BytesRecorded),
            _ => null
        };

        if (samples != null && samples.Length > 0)
            SamplesAvailable?.Invoke(samples);
    }

    private static float[] BytesToFloat32(byte[] buffer, int bytes)
    {
        int count = bytes / 4;
        var samples = new float[count];
        Buffer.BlockCopy(buffer, 0, samples, 0, count * 4);
        return samples;
    }

    private static float[] Pcm16ToFloat(byte[] buffer, int bytes)
    {
        int count = bytes / 2;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            short s = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
            samples[i] = s / 32768f;
        }
        return samples;
    }

    private static float[] Pcm24ToFloat(byte[] buffer, int bytes)
    {
        int count = bytes / 3;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            int b0 = buffer[i * 3 + 0];
            int b1 = buffer[i * 3 + 1];
            int b2 = buffer[i * 3 + 2];

            int v = b0 | (b1 << 8) | (b2 << 16);
            if ((v & 0x00800000) != 0) v |= unchecked((int)0xFF000000); // sign extend
            samples[i] = v / 8388608f;
        }
        return samples;
    }

    private static float[] Pcm32ToFloat(byte[] buffer, int bytes)
    {
        int count = bytes / 4;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            int v = BitConverter.ToInt32(buffer, i * 4);
            samples[i] = v / 2147483648f;
        }
        return samples;
    }

    public void Dispose()
    {
        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.Dispose();
            _capture = null;
        }
    }
}
