using System.Numerics;

namespace Unosquare.Ser2Net.Statistics;

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
