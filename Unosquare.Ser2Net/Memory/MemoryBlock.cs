using System.Runtime.InteropServices;

namespace Unosquare.Ser2Net.Memory;

internal unsafe sealed class MemoryBlock<T>
    where T : unmanaged
{
    private const int DefaultLength = 1024;
    
    public MemoryBlock(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        Count = count;
        Address = (nint)NativeMemory.AllocZeroed((nuint)(count * sizeof(T)));
    }

    public MemoryBlock() : this(DefaultLength)
    {
        // placeholder
    }

    public int ByteLength => sizeof(T) * Count;

    public int Count { get; private set; }

    public nint Address { get; private set; }

    public bool IsDisposed { get; private set; }

    public Span<T> AsSpan() => new(Address.ToPointer(), Count);

    public ReadOnlySpan<T> AsReadOnly() => new(Address.ToPointer(), Count);

    public void Reallocate(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        if (count <= Count)
            return;

        Count = count;
        Address = (nint)NativeMemory.Realloc(
            Address.ToPointer(),
            (nuint)(count * sizeof(T)));
    }

    public void Clear() => NativeMemory.Clear(Address.ToPointer(), (nuint)ByteLength);

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        if (Address != IntPtr.Zero)
            NativeMemory.Free(Address.ToPointer());

        Address = IntPtr.Zero;
        Count = 0;
    }
}
