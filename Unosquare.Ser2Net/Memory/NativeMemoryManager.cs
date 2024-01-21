namespace Unosquare.Ser2Net.Memory;

internal unsafe sealed class NativeMemoryManager<T> : MemoryManager<T>
    where T : unmanaged
{
    public int TotalByteLength;
    public int ElementSize;
    public T* Pointer;
    public int ElementCount;
    private long _IsDisposed;

    public NativeMemoryManager(int elementCount)
    {
        ElementCount = elementCount;
        ElementSize = sizeof(T);
        TotalByteLength = elementCount * ElementSize;
        Pointer = (T*)NativeMemory.AllocZeroed((nuint)ElementCount, (nuint)ElementSize);
    }

    public bool IsDisposed => Interlocked.Read(ref _IsDisposed) != 0;

    public override Span<T> GetSpan() => new(Pointer, ElementCount);

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= ElementCount)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        return new MemoryHandle(Pointer + elementIndex);
    }

    public override void Unpin()
    {
        // no need to unpin because memory is allocated
        // natively
    }

    public void Clear() => NativeMemory.Clear(Pointer, (nuint)TotalByteLength);

    public void Reallocate(int elementCount)
    {
        int oldByteLength = TotalByteLength;
        ElementCount = elementCount;
        TotalByteLength = elementCount * ElementSize;
        Pointer = (T*)NativeMemory.Realloc(Pointer, (nuint)TotalByteLength);
        
        // zero out the allocated memory
        var bytesAdded = TotalByteLength - oldByteLength;
        NativeMemory.Clear(Pointer + oldByteLength, (nuint)bytesAdded);
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Increment(ref _IsDisposed) > 1)
            return;

        if (!disposing)
            return;

        if (Pointer is not null)
            NativeMemory.Free(Pointer);

        Pointer = null;
        ElementCount = 0;
    }
}
