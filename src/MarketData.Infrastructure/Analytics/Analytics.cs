using System;
using System.Collections.Generic;

namespace MarketData.Infrastructure.Analytics
{
    /// <summary>
    /// Fixed-size ring buffer for O(1) moving average computation.
    /// </summary>
    public sealed class MovingAverageBuffer
    {
        private readonly double[] _buffer;
        private int _index;
        private int _count;
        private double _sum;

        public int Capacity { get; }

        public MovingAverageBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            _buffer = new double[capacity];
        }

        public double Add(double value)
        {
            if (_count < Capacity)
            {
                _buffer[_count] = value;
                _sum += value;
                _count++;
                _index = _count % Capacity;
            }
            else
            {
                double old = _buffer[_index];
                _buffer[_index] = value;
                _sum += value - old;
                _index++;
                if (_index == Capacity) _index = 0;
            }
            return _sum / _count;
        }
    }

    /// <summary>
    /// Sliding time window maintaining min and max via monotonic deques.
    /// </summary>
    public sealed class SlidingWindow
    {
        private readonly int _windowMs;
        private readonly MonotonicDeque _minDeque;
        private readonly MonotonicDeque _maxDeque;

        public SlidingWindow(int windowMilliseconds)
        {
            if (windowMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(windowMilliseconds));
            _windowMs = windowMilliseconds;
            _minDeque = new MonotonicDeque(ascending: true);
            _maxDeque = new MonotonicDeque(ascending: false);
        }

        public void AddSample(long timestampMs, double price)
        {
            long cutoff = timestampMs - _windowMs;
            _minDeque.EvictOlderThan(cutoff);
            _maxDeque.EvictOlderThan(cutoff);

            _minDeque.Push(timestampMs, price);
            _maxDeque.Push(timestampMs, price);
        }

        public bool TryGetMinMax(long nowMs, out double min, out double max)
        {
            long cutoff = nowMs - _windowMs;
            _minDeque.EvictOlderThan(cutoff);
            _maxDeque.EvictOlderThan(cutoff);

            var hasMin = _minDeque.TryPeek(out var minEntry);
            var hasMax = _maxDeque.TryPeek(out var maxEntry);

            if (hasMin && hasMax)
            {
                min = minEntry.Price;
                max = maxEntry.Price;
                return true;
            }

            min = max = 0;
            return false;
        }

        private sealed class MonotonicDeque
        {
            private readonly LinkedList<(long Timestamp, double Price)> _dq = new();
            private readonly bool _ascending;

            public MonotonicDeque(bool ascending) => _ascending = ascending;

            public void Push(long ts, double price)
            {
                if (_ascending)
                {
                    while (_dq.Count > 0 && _dq.Last!.Value.Price > price)
                        _dq.RemoveLast();
                }
                else
                {
                    while (_dq.Count > 0 && _dq.Last!.Value.Price < price)
                        _dq.RemoveLast();
                }
                _dq.AddLast((ts, price));
            }

            public void EvictOlderThan(long cutoff)
            {
                while (_dq.Count > 0 && _dq.First!.Value.Timestamp < cutoff)
                    _dq.RemoveFirst();
            }

            public bool TryPeek(out (long Timestamp, double Price) entry)
            {
                if (_dq.Count > 0)
                {
                    entry = _dq.First!.Value;
                    return true;
                }
                entry = default;
                return false;
            }
        }
    }
}
