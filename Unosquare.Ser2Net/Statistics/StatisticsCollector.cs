using System.Numerics;

namespace Unosquare.Ser2Net.Statistics;

/// <summary>
/// A class that serves as a collector for performance statistics.
/// Storage is implemented as a <see cref="MemoryQueue{T}"/> and
/// statistics are computed in a synchronized fashion for efficiency.
/// </summary>
/// <typeparam name="T">Generic type parameter.</typeparam>
internal class StatisticsCollector<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    private const int DefaultMaxDataPoints = 1000;
    private readonly object SyncRoot = new();
    private readonly MemoryQueue<Sample<T>> SamplesQueue;
    private readonly bool IgnoreZeroes;
    private readonly int m_Capacity;

    #region Synchronized computed state variables

    private long? sLifetimeTimestamp;
    private double sLifetimeSamplesSum;
    private long sLifetimeSampleCount;

    private int sCurrentSampleCount;

    private double sCurrentElapsedSum;
    private double? sCurrentElapsedMin;
    private double? sCurrentElapsedMax;

    private double sCurrentSamplesSum;
    private double? sCurrentSamplesMin;
    private double? sCurrentSamplesMax;

    private double sCurrentRatesSum;
    private double? sCurrentRatesMin;
    private double? sCurrentRatesMax;

    private long? sCurrentTimestampMin;
    private long? sCurrentTimestampMax;
    private TimeSpan? sCurrentNaturalElapsed;

    #endregion

    public StatisticsCollector(bool ignoreZeroes, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);

        IgnoreZeroes = ignoreZeroes;
        m_Capacity = capacity;
        SamplesQueue = new(capacity);
    }

    public StatisticsCollector(bool ignoreZeroes)
        : this(ignoreZeroes, DefaultMaxDataPoints)
    {
        // placeholder
    }

    public StatisticsCollector() : this(false, DefaultMaxDataPoints)
    {
        // placeholder
    }

    /// <summary>
    /// Gets the capacity of the current samples queue.
    /// </summary>
    public int Capacity => m_Capacity;

    /// <summary>
    /// Gets the total elasped time since this collector started recording samples.
    /// </summary>
    public TimeSpan LifetimeElapsed
    {
        get
        {
            lock (SyncRoot)
                return !sLifetimeTimestamp.HasValue
                    ? TimeSpan.Zero
                    : Stopwatch.GetElapsedTime(sLifetimeTimestamp.Value);
        }
    }

    /// <summary>
    /// Gets the total unchecked sum of all the sample values that have been
    /// sent to this collector regardles of their current availability.
    /// </summary>
    public double LifetimeSamplesSum
    {
        get
        {
            lock (SyncRoot)
                return sLifetimeSamplesSum;
        }
    }

    /// <summary>
    /// Gets the total unchecked count of all the samples that have been
    /// sent to this collector regardles of their current availability.
    /// </summary>
    public long LifetimeSampleCount
    {
        get
        {
            lock (SyncRoot)
                return sLifetimeSampleCount;
        }
    }

    /// <summary>
    /// Gets the number of currenlty recorded samples.
    /// </summary>
    public int CurrentSampleCount
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount;
        }
    }

    /// <summary>
    /// Gets the sum of <see cref="Sample{T}.ElapsedSeconds"/> of
    /// the currently available samples.
    /// </summary>
    /// <remarks>
    /// This computes on discrete <see cref="Sample{T}.ElapsedSeconds"/>
    /// intervals. For total time elapsed of current samples, use
    /// <see cref="CurrentNaturalElapsed"/> instead.
    /// </remarks>
    public TimeSpan? CurrentElapsedSum
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(sCurrentElapsedSum * 1000d);
        }
    }

    /// <summary>
    /// Gets the average <see cref="Sample{T}.ElapsedSeconds"/> of
    /// the currently available samples.
    /// </summary>
    /// <remarks>
    /// This computes on discrete <see cref="Sample{T}.ElapsedSeconds"/>
    /// intervals. For total time elapsed of current samples, use
    /// <see cref="CurrentNaturalElapsed"/> instead.
    /// </remarks>
    public TimeSpan? CurrentElapsedAverage
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(sCurrentElapsedSum * 1000d / sCurrentSampleCount);
        }
    }

    /// <summary>
    /// Gets the minimum <see cref="Sample{T}.ElapsedSeconds"/> of
    /// the currently available samples.
    /// </summary>
    /// <remarks>
    /// This computes on discrete <see cref="Sample{T}.ElapsedSeconds"/>
    /// intervals. For total time elapsed of current samples, use
    /// <see cref="CurrentNaturalElapsed"/> instead.
    /// </remarks>
    public TimeSpan? CurrentElapsedMin
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 || !sCurrentElapsedMin.HasValue
                    ? default : TimeSpan.FromMilliseconds(sCurrentElapsedMin.Value * 1000d);
        }
    }

    /// <summary>
    /// Gets the maximum <see cref="Sample{T}.ElapsedSeconds"/> of
    /// the currently available samples.
    /// </summary>
    /// <remarks>
    /// This computes on discrete <see cref="Sample{T}.ElapsedSeconds"/>
    /// intervals. For total time elapsed of current samples, use
    /// <see cref="CurrentNaturalElapsed"/> instead.
    /// </remarks>
    public TimeSpan? CurrentElapsedMax
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 || !sCurrentElapsedMax.HasValue
                    ? default : TimeSpan.FromMilliseconds(sCurrentElapsedMax.Value * 1000d);
        }
    }

    /// <summary>
    /// Gets the sum of <see cref="Sample{T}.DoubleValue"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentSamplesSum
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : sCurrentSamplesSum;
        }
    }

    /// <summary>
    /// Gets the average <see cref="Sample{T}.DoubleValue"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentSamplesAverage
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : sCurrentSamplesSum / sCurrentSampleCount;
        }
    }

    /// <summary>
    /// Gets the minimum of <see cref="Sample{T}.DoubleValue"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentSamplesMin
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSamplesMin;
        }
    }

    /// <summary>
    /// Gets the maximum of <see cref="Sample{T}.DoubleValue"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentSamplesMax
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSamplesMax;
        }
    }

    /// <summary>
    /// Gets the sum of <see cref="Sample{T}.Rate"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentRatesSum
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : sCurrentRatesSum;
        }
    }

    /// <summary>
    /// Gets the average <see cref="Sample{T}.Rate"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentRatesAverage
    {
        get
        {
            lock (SyncRoot)
                return sCurrentSampleCount <= 0 ? default : sCurrentRatesSum / sCurrentSampleCount;
        }
    }

    /// <summary>
    /// Gets the minimum <see cref="Sample{T}.Rate"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentRatesMin
    {
        get
        {
            lock (SyncRoot)
                return sCurrentRatesMin;
        }
    }

    /// <summary>
    /// Gets the maximum <see cref="Sample{T}.Rate"/> of
    /// the currently available samples.
    /// </summary>
    public double? CurrentRatesMax
    {
        get
        {
            lock (SyncRoot)
                return sCurrentRatesMax;
        }
    }

    /// <summary>
    /// Gets the minimum <see cref="Sample{T}.StartTimestamp"/> of
    /// the currently available samples.
    /// </summary>
    public long? CurrentTimestampMin
    {
        get
        {
            lock (SyncRoot)
                return sCurrentTimestampMin;
        }
    }

    /// <summary>
    /// Gets the maximum <see cref="Sample{T}.StartTimestamp"/> of
    /// the currently available samples.
    /// </summary>
    public long? CurrentTimestampMax
    {
        get
        {
            lock (SyncRoot)
                return sCurrentTimestampMax;
        }
    }

    /// <summary>
    /// Gets the time elapsed from the currently available samples
    /// using the time difference between <see cref="CurrentTimestampMin"/>
    /// and <see cref="CurrentTimestampMax"/> plus the <see cref="Sample{T}.ElapsedSeconds"/>
    /// of the last sample.
    /// </summary>
    public TimeSpan? CurrentNaturalElapsed
    {
        get
        {
            lock (SyncRoot)
                return sCurrentNaturalElapsed;
        }
    }

    /// <summary>
    /// Gets the natural rate of the currently availlable samples.
    /// <see cref="CurrentSamplesSum"/> / <see cref="sCurrentNaturalElapsed"/>
    /// </summary>
    public double? CurrentNaturalRate
    {
        get
        {
            lock (SyncRoot)
            {
                if (!sCurrentNaturalElapsed.HasValue)
                    return default;

                return sCurrentSamplesSum / sCurrentNaturalElapsed.Value.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// Gets all the currently available samples.
    /// </summary>
    public IReadOnlyList<Sample<T>> Samples => SamplesQueue.Peek(-1);

    /// <summary>
    /// Begins a time measurment on a sample.
    /// Use <see cref="ISampleRecorder{T}.Record(T)"/> to
    /// stop the time measurement and record the value.
    /// </summary>
    /// <returns>
    /// A disposable recorder that submits a sample value.
    /// </returns>
    public ISampleRecorder<T> BeginSample()
    {
        sLifetimeTimestamp ??= Stopwatch.GetTimestamp();
        return new Recorder(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SamplesQueue.Dispose();
    }

    private void RecomputeStatistics()
    {
        lock (SyncRoot)
        {
            // reset stats
            sCurrentSampleCount = default;

            sCurrentElapsedSum = default;
            sCurrentElapsedMin = default;
            sCurrentElapsedMax = default;

            sCurrentSamplesSum = default;
            sCurrentSamplesMin = default;
            sCurrentSamplesMax = default;

            sCurrentRatesSum = default;
            sCurrentRatesMin = default;
            sCurrentRatesMax = default;

            sCurrentTimestampMin = default;
            sCurrentTimestampMax = default;
            sCurrentNaturalElapsed = default;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            sCurrentSampleCount = SamplesQueue.Peek(samples);
            if (sCurrentSampleCount <= 0)
                return;

            var lastSampleElapsedSecs = -1d;

            foreach (var sample in samples[0..sCurrentSampleCount])
            {
                sCurrentElapsedMin ??= sample.ElapsedSeconds;
                sCurrentElapsedMax ??= sample.ElapsedSeconds;
                sCurrentSamplesMin ??= sample.DoubleValue;
                sCurrentSamplesMax ??= sample.DoubleValue;
                sCurrentRatesMin ??= sample.Rate;
                sCurrentRatesMax ??= sample.Rate;
                sCurrentTimestampMin ??= sample.StartTimestamp;
                sCurrentTimestampMax ??= sample.StartTimestamp;

                if (sample.ElapsedSeconds < sCurrentElapsedMin.Value)
                    sCurrentElapsedMin = sample.ElapsedSeconds;

                if (sample.ElapsedSeconds > sCurrentElapsedMax.Value)
                    sCurrentElapsedMax = sample.ElapsedSeconds;

                if (sample.DoubleValue < sCurrentSamplesMin.Value)
                    sCurrentSamplesMin = sample.DoubleValue;

                if (sample.DoubleValue > sCurrentSamplesMax.Value)
                    sCurrentSamplesMax = sample.DoubleValue;

                if (sample.Rate < sCurrentRatesMin.Value)
                    sCurrentRatesMin = sample.Rate;

                if (sample.Rate > sCurrentRatesMax.Value)
                    sCurrentRatesMax = sample.Rate;

                if (sample.StartTimestamp < sCurrentTimestampMin.Value)
                    sCurrentTimestampMin = sample.StartTimestamp;

                if (sample.StartTimestamp > sCurrentTimestampMax.Value)
                {
                    sCurrentTimestampMax = sample.StartTimestamp;
                    lastSampleElapsedSecs = sample.ElapsedSeconds;
                }

                sCurrentElapsedSum += sample.ElapsedSeconds;
                sCurrentSamplesSum += sample.DoubleValue;
                sCurrentRatesSum += sample.Rate;
            }

            if (sCurrentTimestampMin.HasValue && sCurrentTimestampMax.HasValue && lastSampleElapsedSecs != -1)
            {
                sCurrentNaturalElapsed = Stopwatch.GetElapsedTime(sCurrentTimestampMin.Value, sCurrentTimestampMax.Value);
                sCurrentNaturalElapsed = sCurrentNaturalElapsed.Value.Add(
                    TimeSpan.FromMilliseconds(lastSampleElapsedSecs * 1000d));
            }
        }
    }

    private record struct Recorder : ISampleRecorder<T>
    {
        private readonly StatisticsCollector<T> Target;
        private long HasRecorded;
        public long StartTimestamp = Stopwatch.GetTimestamp();

        public Recorder(StatisticsCollector<T> target)
        {
            Target = target;
        }

        public void Record(T sampleValue)
        {
            if (Interlocked.Increment(ref HasRecorded) > 1)
                throw new InvalidOperationException(
                    $"{nameof(Record)} can only be called once in the lifetime of this {nameof(ISampleRecorder<T>)}.");

            if (Target.IgnoreZeroes && T.IsZero(sampleValue))
                return;

            lock (Target.SyncRoot)
            {
                var sample = new Sample<T>(StartTimestamp, sampleValue);

                // make room if needed.
                if (Target.SamplesQueue.Length >= Target.Capacity)
                    Target.SamplesQueue.Clear(1);

                // enqueue the new sample
                Target.SamplesQueue.Enqueue(sample);

                unchecked
                {
                    Target.sLifetimeSamplesSum += sample.DoubleValue;
                    Target.sLifetimeSampleCount += 1;
                }

                Target.RecomputeStatistics();
            }
        }

        public readonly void Dispose()
        {
            // placeholder
        }
    }
}
