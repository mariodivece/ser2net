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

    private long? currentTimestampMin;
    private long? currentTimestampMax;
    private TimeSpan? currentNaturalElapsed;

    public StatisticsCollector() : this(false)
    {
        // placeholder
    }

    public StatisticsCollector(bool ignoreZeroes)
    {
        IgnoreZeroes = ignoreZeroes;
    }

    public ISampleRecorder<T> BeginSample()
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
                return currentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(currentElapsedSum * 1000d);
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
                return currentSampleCount <= 0 ? default : TimeSpan.FromMilliseconds(currentElapsedSum * 1000d / currentSampleCount);
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
                return currentSampleCount <= 0 || !currentElapsedMin.HasValue
                    ? default : TimeSpan.FromMilliseconds(currentElapsedMin.Value * 1000d);
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
                return currentSampleCount <= 0 || !currentElapsedMax.HasValue
                    ? default : TimeSpan.FromMilliseconds(currentElapsedMax.Value * 1000d);
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
                return currentSampleCount <= 0 ? default : currentSamplesSum;
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
                return currentSampleCount <= 0 ? default : currentSamplesSum / currentSampleCount;
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
                return currentSamplesMin;
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
                return currentSamplesMax;
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
                return currentSampleCount <= 0 ? default : currentRatesSum;
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
                return currentSampleCount <= 0 ? default : currentRatesSum / currentSampleCount;
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
                return currentRatesMin;
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
                return currentRatesMax;
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
                return currentTimestampMin;
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
                return currentTimestampMax;
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
                return currentNaturalElapsed;
        }
    }

    /// <summary>
    /// Gets the natural rate of the currently availlable samples.
    /// <see cref="CurrentSamplesSum"/> / <see cref="currentNaturalElapsed"/>
    /// </summary>
    public double? CurrentNaturalRate
    {
        get
        {
            lock (SyncRoot)
            {
                if (!currentNaturalElapsed.HasValue)
                    return default;

                return currentSamplesSum / currentNaturalElapsed.Value.TotalSeconds;
            }
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

        public readonly void Dispose()
        {
            // placeholder
        }
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

            currentTimestampMin = default;
            currentTimestampMax = default;
            currentNaturalElapsed = default;

            Span<Sample<T>> samples = stackalloc Sample<T>[SamplesQueue.Length];
            currentSampleCount = SamplesQueue.Peek(samples);
            if (currentSampleCount <= 0)
                return;

            var lastSampleElapsedSecs = -1d;

            foreach (var sample in samples[0..currentSampleCount])
            {
                currentElapsedMin ??= sample.ElapsedSeconds;
                currentElapsedMax ??= sample.ElapsedSeconds;
                currentSamplesMin ??= sample.DoubleValue;
                currentSamplesMax ??= sample.DoubleValue;
                currentRatesMin ??= sample.Rate;
                currentRatesMax ??= sample.Rate;
                currentTimestampMin ??= sample.StartTimestamp;
                currentTimestampMax ??= sample.StartTimestamp;

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

                if (sample.StartTimestamp < currentTimestampMin.Value)
                    currentTimestampMin = sample.StartTimestamp;

                if (sample.StartTimestamp > currentTimestampMax.Value)
                {
                    currentTimestampMax = sample.StartTimestamp;
                    lastSampleElapsedSecs = sample.ElapsedSeconds;
                }

                currentElapsedSum += sample.ElapsedSeconds;
                currentSamplesSum += sample.DoubleValue;
                currentRatesSum += sample.Rate;
            }

            if (currentTimestampMin.HasValue && currentTimestampMax.HasValue && lastSampleElapsedSecs != -1)
            {
                currentNaturalElapsed = Stopwatch.GetElapsedTime(currentTimestampMin.Value, currentTimestampMax.Value);
                currentNaturalElapsed = currentNaturalElapsed.Value.Add(
                    TimeSpan.FromMilliseconds(lastSampleElapsedSecs * 1000d));
            }
        }
    }
}

internal readonly struct Sample<T>
    where T : unmanaged, INumber<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sample&lt;T&gt;"/> struct.
    /// </summary>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value marking the start
    /// of the sample recording.</param>
    /// <param name="value">The recorded sample value.</param>
    public Sample(long startTimestamp, T value)
    {
        StartTimestamp = startTimestamp;
        ElapsedSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        Value = value;
        DoubleValue = ToDouble(value);
        Rate = DoubleValue / ElapsedSeconds;
    }

    /// <summary>
    /// The <see cref="Stopwatch.GetTimestamp"/> value
    /// marking the start of the sample recording.
    /// </summary>
    public readonly long StartTimestamp;

    /// <summary>
    /// The elapsed time in seconds of this sample recording.
    /// </summary>
    public readonly double ElapsedSeconds;

    /// <summary>
    /// The <see cref="DoubleValue"/> divided by <see cref="ElapsedSeconds"/>.
    /// </summary>
    public readonly double Rate;

    /// <summary>
    /// The <see cref="Value"/> as a <see cref="double"/>
    /// </summary>
    public readonly double DoubleValue;

    /// <summary>
    /// The recorded sample value.
    /// </summary>
    public readonly T Value;

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
    void Record(T sampleValue);
}