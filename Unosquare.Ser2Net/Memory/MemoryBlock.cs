namespace Unosquare.Ser2Net.Memory;

/// <summary>
/// Represents a contiguous region of pre-allocated, unmanaged memory
/// that can be used as <see cref="Memory{T}"/> or <see cref="Span{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements to hold.</typeparam>
internal unsafe sealed class MemoryBlock<T> : IDisposable
    where T : unmanaged
{
    private const int DefaultLength = 1024;
    private readonly NativeMemoryManager MemoryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryBlock{T}"/> class.
    /// </summary>
    /// <param name="length">The length in number of elements that this block can hold.</param>
    public MemoryBlock(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0);
        MemoryManager = new NativeMemoryManager(length);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryBlock{T}"/> class.
    /// </summary>
    public MemoryBlock() : this(DefaultLength)
    {
        // placeholder
    }

    /// <summary>
    /// Gets the byte length of the allocated memory block.
    /// </summary>
    public int ByteLength => sizeof(T) * Length;

    /// <summary>
    /// Gets the length in number of elements that this block can hold.
    /// </summary>
    public int Length => MemoryManager.Length;

    /// <summary>
    /// Gets the unmanaged pointer to the allocated native memory.
    /// </summary>
    public T* Pointer => MemoryManager.Pointer;

    /// <summary>
    /// Gets the <see cref="nint"/> representation of the <see cref="Pointer"/> value.
    /// </summary>
    public nint Address => new(Pointer);

    /// <summary>
    /// Gets a value indicating whether this instance is disposed.
    /// </summary>
    public bool IsDisposed => MemoryManager.IsDisposed;

    /// <summary>
    /// Gets a <see cref="Span{T}"/> from the native memory block.
    /// </summary>
    public Span<T> Span => MemoryManager.GetSpan();

    /// <summary>
    /// Gets a <see cref="Memory{T}"/> from the native memory block.
    /// </summary>
    public Memory<T> Memory => MemoryManager.Memory;

    /// <summary>
    /// <seealso cref="Span{T}.Slice(int)"/>
    /// </summary>
    public Span<T> Slice(int start) => MemoryManager.GetSpan()[start..];

    /// <summary>
    /// <seealso cref="Span{T}.Slice(int, int)"/>
    /// </summary>
    public Span<T> Slice(int start, int length) => MemoryManager.GetSpan().Slice(start, length);

    public int CopyTo(int startOffset, void* target, int count)
    {
        if (count == 0 || startOffset >= Length || target is null)
            return 0;

        var maxCount = Length - startOffset;
        var elementCount = count < 0 || count > maxCount ? maxCount : count;
        var copyByteLength = (nuint)(elementCount * sizeof(T));
        var sourceAddress = Address + (startOffset * sizeof(T));

        NativeMemory.Copy(sourceAddress.ToPointer(), target, copyByteLength);
        return elementCount;
    }

    public int CopyTo(int startOffset, MemoryBlock<T> target, int targetOffset, int count)
    {
        if (count == 0 || target is null || startOffset >= Length || targetOffset >= target.Length)
            return 0;

        var maxSourceCount = Length - startOffset;
        var maxTargetCount = target.Length - targetOffset;
        var maxCount = Math.Min(maxSourceCount, maxTargetCount);
        var elementCount = count < 0 || count > maxCount ? maxCount : count;

        Slice(startOffset, elementCount)
            .CopyTo(target.Slice(targetOffset));

        return elementCount;
    }

    public int CopyTo(int startOffset, Span<T> target, int count)
    {
        if (count == 0 || startOffset >= Length || target.Length <= 0)
            return 0;

        var maxSourceCount = Length - startOffset;
        var maxTargetCount = target.Length;
        var maxCount = Math.Min(maxSourceCount, maxTargetCount);
        var elementCount = count < 0 || count > maxCount ? maxCount : count;

        Slice(startOffset, elementCount).CopyTo(target);
        return elementCount;
    }

    public int CopyTo(int startOffset, Span<T> target, int targetOffset, int count) =>
        CopyTo(startOffset, target[targetOffset..], count);

    public void Clear() => NativeMemory.Clear(Address.ToPointer(), (nuint)ByteLength);

    /// <summary>
    /// Releases the unmanaged resources used by the MemoryBlock and optionally releases the
    /// managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    /// release only unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        if (!disposing)
            return;

        if (MemoryManager is IDisposable manager)
            manager.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private sealed class NativeMemoryManager(int length) : MemoryManager<T>
    {
        public T* Pointer = (T*)NativeMemory.AllocZeroed((nuint)(length * sizeof(T)));
        public int Length = length;
        public bool IsDisposed;

        public override Span<T> GetSpan() => new(Pointer, Length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= Length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            return new MemoryHandle(Pointer + elementIndex);
        }

        public override void Unpin()
        {
            // noop
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            if (!disposing)
                return;

            if (Pointer is not null)
                NativeMemory.Free(Pointer);

            Pointer = null;
            Length = 0;
        }
    }
}
