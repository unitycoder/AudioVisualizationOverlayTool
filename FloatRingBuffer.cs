namespace AudioFXOverlay
{
    public sealed class FloatRingBuffer
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public FloatRingBuffer(int capacity)
        {
            _buffer = new float[Math.Max(1, capacity)];
        }

        public void Write(ReadOnlySpan<float> src)
        {
            int n = src.Length;
            if (n >= _buffer.Length)
            {
                // Keep only the tail that fits.
                src = src.Slice(n - _buffer.Length);
                n = src.Length;
            }

            int first = Math.Min(n, _buffer.Length - _writeIndex);
            src.Slice(0, first).CopyTo(_buffer.AsSpan(_writeIndex, first));

            int remaining = n - first;
            if (remaining > 0)
                src.Slice(first, remaining).CopyTo(_buffer.AsSpan(0, remaining));

            _writeIndex = (_writeIndex + n) % _buffer.Length;
            _count = Math.Min(_buffer.Length, _count + n);
        }

        // Copy newest 'count' samples into dst (dst.Length must be >= count).
        public void ReadLatest(Span<float> dst, int count)
        {
            count = Math.Min(count, _count);
            if (count <= 0) return;

            int end = _writeIndex; // points to next write, so newest ends here
            int start = end - count;
            if (start < 0) start += _buffer.Length;

            int first = Math.Min(count, _buffer.Length - start);
            _buffer.AsSpan(start, first).CopyTo(dst.Slice(0, first));

            int remaining = count - first;
            if (remaining > 0)
                _buffer.AsSpan(0, remaining).CopyTo(dst.Slice(first, remaining));
        }
    }

}
