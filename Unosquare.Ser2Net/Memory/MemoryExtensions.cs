namespace Unosquare.Ser2Net.Memory;

internal static unsafe class MemoryExtensions
{
    public static int CopyTo<T>(this ReadOnlySpan<T> source, MemoryBlock<T> target, int targetOffset, int count)
        where T : unmanaged
    {
        if (count == 0 || target is null || targetOffset >= target.Length || source.Length <= 0)
            return 0;

        var maxSourceCount = source.Length;
        var maxTargetCount = target.Length - targetOffset;
        var maxCount = Math.Min(maxSourceCount, maxTargetCount);
        var elementCount = count < 0 || count > maxCount ? maxCount : count;

        source.CopyTo(target.Slice(targetOffset, elementCount));
        return elementCount;
    }

    public static int CopyTo<T>(this ReadOnlySpan<T> source, int sourceOffset, MemoryBlock<T> target, int targetOffset, int count)
        where T : unmanaged =>
        source[sourceOffset..].CopyTo(target, targetOffset, count);
}
