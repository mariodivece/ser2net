using System.Numerics;

namespace Unosquare.Ser2Net.Services;

internal class StatisticsCollector<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    private const int MaxDataPoints = 1000;
    private readonly object SyncRoot = new();
    private readonly MemoryQueue<Sample<T>> SamplesQueue = new(MaxDataPoints);
    private readonly bool IgnoreZeroes;

    private long? LifetimeTimestamp;
    private ulong m_TotalSampleCount;
    private double m_LifetimeSamplesSum;
    private long m_LifetimeSampleCount;

    private int currentSampleCount;

    private double currentElapsedSum;
    private double? currentElapsedMin;
    private double? currentElapsedMax;

    private double currentSamplesSum;
    private double? currentSamplesMin;
    private double? currentSamplesMax;

    private double currentRatesSum;
    private double? currentRatesMin;
    private double? currentRatesMax;

    public StatisticsCollector(bool ignoreZeroes)
    {
        IgnoreZeroes = ignoreZeroes;
    }

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
    public double LifetimeSamplesSum
    {
        get
        {
            lock (SyncRoot)
                return m_LifetimeSamplesSum;
        }
    }

    public long LifetimeSampleCount
    {
        get
        {
            lock (SyncRoot)
                return m_LifetimeSampleCount;
        }
    }

    public int CurrentSampleCount
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount;
        }
    }

    public TimeSpan? CurrentElapsedSum
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(currentElapsedSum * 1000d);
        }
    }

    public TimeSpan? CurrentElapsedAverage
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(currentElapsedSum * 1000d / currentSampleCount);
        }
    }

    public TimeSpan? CurrentElapsedMin
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 || !currentElapsedMin.HasValue
                    ? default : TimeSpan.FromMilliseconds(currentElapsedMin.Value * 1000d);
        }
    }

    public TimeSpan? CurrentElapsedMax
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 || !currentElapsedMax.HasValue
                    ? default : TimeSpan.FromMilliseconds(currentElapsedMax.Value * 1000d);
        }
    }

    public double? CurrentSamplesSum
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : currentSamplesSum;
        }
    }

    public double? CurrentSamplesAverage
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : currentSamplesSum / currentSampleCount;
        }
    }

    public double? CurrentSamplesMin
    {
        get
        {
            lock (SyncRoot)
                return currentSamplesMin;
        }
    }

    public double? CurrentSamplesMax
    {
        get
        {
            lock (SyncRoot)
                return currentSamplesMax;
        }
    }

    public double? CurrentRatesSum
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : currentRatesSum;
        }
    }

    public double? CurrentRatesAverage
    {
        get
        {
            lock (SyncRoot)
                return currentSampleCount <= 0 ? default : currentRatesSum / currentSampleCount;
        }
    }

    public double? CurrentRatesMin
    {
        get
        {
            lock (SyncRoot)
                return currentRatesMin;
        }
    }

    public double? CurrentRatesMax
    {
        get
        {
            lock (SyncRoot)
                return currentRatesMax;
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

    private void RecomputeStatistics()
    {
        lock (SyncRoot)
        {
            // reset stats
            currentSampleCount = default;

            currentElapsedSum = default;
            currentElapsedMin = default;
            currentElapsedMax = default;

            currentSamplesSum = default;
            currentSamplesMin = default;
            currentSamplesMax = default;

            currentRatesSum = default;
            currentRatesMin = default;
            currentRatesMax = default;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            currentSampleCount = SamplesQueue.Peek(samples);
            if (currentSampleCount <= 0)
                return;

            foreach (var sample in samples[0..currentSampleCount])
            {
                currentElapsedMin ??= sample.ElapsedSeconds;
                currentElapsedMax ??= sample.ElapsedSeconds;
                currentSamplesMin ??= sample.DoubleValue;
                currentSamplesMax ??= sample.DoubleValue;
                currentRatesMin ??= sample.Rate;
                currentRatesMax ??= sample.Rate;

                if (sample.ElapsedSeconds < currentElapsedMin.Value)
                    currentElapsedMin = sample.ElapsedSeconds;

                if (sample.ElapsedSeconds > currentElapsedMax.Value)
                    currentElapsedMax = sample.ElapsedSeconds;

                if (sample.DoubleValue < currentSamplesMin.Value)
                    currentSamplesMin = sample.DoubleValue;

                if (sample.DoubleValue > currentSamplesMax.Value)
                    currentSamplesMax = sample.DoubleValue;

                if (sample.Rate < currentRatesMin.Value)
                    currentRatesMin = sample.Rate;

                if (sample.Rate > currentRatesMax.Value)
                    currentRatesMax = sample.Rate;

                currentElapsedSum += sample.ElapsedSeconds;
                currentSamplesSum += sample.DoubleValue;
                currentRatesSum += sample.Rate;
            }
        }
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

            if (Target.IgnoreZeroes && T.IsZero(sampleValue))
                return;

            lock (Target.SyncRoot)
            {
                var elapsed = Stopwatch.GetElapsedTime(StartTimestamp).TotalSeconds;
                var sample = new Sample<T>(elapsed, sampleValue);
                if (Target.SamplesQueue.Length >= MaxDataPoints)
                    Target.SamplesQueue.Clear(1);

                Target.SamplesQueue.Enqueue(sample);

                Interlocked.Increment(ref Target.m_TotalSampleCount);

                unchecked
                {
                    Target.m_LifetimeSamplesSum += sample.DoubleValue;
                    Target.m_LifetimeSampleCount += 1;
                }

                Target.RecomputeStatistics();
            }
        }

        public void Dispose()
        {
            // placeholder
        }
    }

}

internal readonly struct Sample<T>
    where T : unmanaged, INumber<T>
{
    public Sample(double elapsedSeconds, T value)
    {
        ElapsedSeconds = elapsedSeconds;
        Value = value;
        DoubleValue = ToDouble(value);
        Rate = DoubleValue / elapsedSeconds;
    }

    public readonly double ElapsedSeconds;
    public readonly T Value;
    public readonly double Rate;
    public readonly double DoubleValue;

    private static double ToDouble(T val) => val is double doubleVal
        ? doubleVal
        : val is int intVal
        ? intVal
        : val is long longVal
        ? longVal
        : val is decimal decimalVal
        ? System.Convert.ToDouble(decimalVal)
        : val is short shortVal
        ? shortVal
        : val is float floatVal
        ? floatVal
        : val is uint uintVal
        ? uintVal
        : val is ulong ulongVal
        ? ulongVal
        : val is ushort ushortVal
        ? ushortVal
        : val is byte byteVal
        ? byteVal
        : double.Parse(val.ToString() ?? "0", CultureInfo.InvariantCulture);
}

internal interface ISampleRecorder<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    void Commit(T sampleValue);
}