using System.Numerics;

namespace Unosquare.Ser2Net.Statistics;

public interface ISampleRecorder<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    void Record(T sampleValue);
}


/// <summary>
/// A non-generic version of the <see cref="ISampleRecorder{T}"/>
/// where the sample value type is <see cref="double"/>.
/// </summary>
public interface ISampleRecorder : ISampleRecorder<double>
{
    // placeholder
}