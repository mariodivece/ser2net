using System.Numerics;

namespace Unosquare.Ser2Net.Services;

internal class StatisticsCollector<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    private const int MaxDataPoints = 1000;
    private readonly object SyncRoot = new();
    private readonly MemoryQueue<Sample<T>> SamplesQueue = new(MaxDataPoints);
    private long? LifetimeTimestamp;
    private ulong m_TotalSampleCount;
    private T m_LifetimeSamplesSum;

    public ISampleRecorder<T> Begin()
    {
        LifetimeTimestamp ??= Stopwatch.GetTimestamp();
        return new Recorder(this);
    }

    /// <summary>
    /// Gets the total elasped time since this collector started recording samples.
    /// </summary>
    public TimeSpan LifetimeElapsed => !LifetimeTimestamp.HasValue
        ? TimeSpan.Zero
        : Stopwatch.GetElapsedTime(LifetimeTimestamp.Value);

    /// <summary>
    /// Gets the total unchecked sum of all the sample values that have been,
    /// sent to this collector regardles of their current availability.
    /// </summary>
    public T LifetimeSamplesSum
    {
        get
        {
            lock (SyncRoot)
                return m_LifetimeSamplesSum;
        }
    }

    public TimeSpan? CurrentElapsedSum
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);
            var totalSeconds = 0d;
            foreach (var sample in samples)
                totalSeconds += sample.ElapsedSeconds;

            return TimeSpan.FromMilliseconds(totalSeconds * 1000d);
        }
    }

    public TimeSpan? CurrentElapsedAverage
    {
        get
        {
            var currentSum = CurrentElapsedSum;
            var count = SamplesQueue.Length;

            if (!currentSum.HasValue || count <= 0)
                return null;

            return TimeSpan.FromMilliseconds(
                currentSum.Value.TotalMilliseconds / count);
        }
    }

    public TimeSpan? CurrentElapsedMin
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);

            
            var result = samples[0].ElapsedSeconds;
            foreach (var sample in samples)
            {
                if (sample.ElapsedSeconds < result)
                    result = sample.ElapsedSeconds;
            }

            return TimeSpan.FromMilliseconds(result * 1000d);
        }
    }

    public TimeSpan? CurrentElapsedMax
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);

            var result = samples[0].ElapsedSeconds;
            foreach (var sample in samples)
            {
                if (sample.ElapsedSeconds > result)
                    result = sample.ElapsedSeconds;
            }

            return TimeSpan.FromMilliseconds(result * 1000d);
        }
    }

    public T? CurrentSamplesSum
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);
            T accumValue = default;
            foreach (var sample in samples)
                accumValue += sample.Value;

            return accumValue;
        }
    }

    public double? CurrentSamplesAverage
    {
        get
        {
            var currentSum = CurrentSamplesSum;
            var count = SamplesQueue.Length;

            if (!currentSum.HasValue || count <= 0)
                return null;

            var doubleSum = double.Parse(currentSum.Value.ToString()!, CultureInfo.InvariantCulture);

            return doubleSum / count;
        }
    }

    public T? CurrentSamplesMin
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);

            var result = samples[0].Value;
            foreach (var sample in samples)
            {
                if (sample.Value < result)
                    result = sample.Value;
            }

            return result;
        }
    }

    public T? CurrentSamplesMax
    {
        get
        {
            if (SamplesQueue.Length <= 0)
                return null;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            SamplesQueue.Peek(samples);

            var result = samples[0].Value;
            foreach (var sample in samples)
            {
                if (sample.Value > result)
                    result = sample.Value;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets all the currently available samples.
    /// </summary>
    public IReadOnlyList<Sample<T>> Samples => SamplesQueue.Peek(-1);

    public void Dispose()
    {
        SamplesQueue.Dispose();
    }

    private record struct Recorder : ISampleRecorder<T>
    {
        private readonly StatisticsCollector<T> Target;
        private long HasCommitted;
        public long StartTimestamp = Stopwatch.GetTimestamp();

        public Recorder(StatisticsCollector<T> target)
        {
            Target = target;
        }

        public void Commit(T sampleValue)
        {
            if (Interlocked.Increment(ref HasCommitted) > 1)
                return;

            var elapsed = Stopwatch.GetElapsedTime(StartTimestamp).TotalSeconds;
            if (Target.SamplesQueue.Length >= MaxDataPoints)
                Target.SamplesQueue.Clear(1);

            Target.SamplesQueue.Enqueue(new Sample<T>(elapsed, sampleValue));

            Interlocked.Increment(ref Target.m_TotalSampleCount);
            lock (Target.SyncRoot)
            {
                unchecked
                {
                    Target.m_LifetimeSamplesSum += sampleValue;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Read(ref HasCommitted) == 0)
                throw new InvalidOperationException($"Unable to record datum without a call to '{nameof(Commit)}'");
        }
    }

}

internal readonly struct Sample<T>(double elapsedSeconds, T value)
    where T : unmanaged, INumber<T>
{
    public readonly double ElapsedSeconds = elapsedSeconds;
    public readonly T Value = value;
}

internal interface ISampleRecorder<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    void Commit(T sampleValue);
}