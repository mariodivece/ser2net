namespace Unosquare.Ser2Net.Memory;

/// <summary>
/// Represents a contiguous region of pre-allocated, unmanaged memory
/// that can be used as <see cref="Memory{T}"/> or <see cref="Span{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements to hold.</typeparam>
public sealed class MemoryBlock<T> : IDisposable
    where T : unmanaged
{
    private const int DefaultLength = 32;
    private readonly NativeMemoryManager<T> MemoryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryBlock{T}"/> class.
    /// </summary>
    /// <param name="elementCount">The length in number of elements that this block can hold.</param>
    public MemoryBlock(int elementCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(elementCount, 0);
        MemoryManager = new(elementCount);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryBlock{T}"/> class.
    /// </summary>
    public MemoryBlock() : this(DefaultLength)
    {
        // placeholder
    }

    /// <summary>
    /// Gets the total byte length of the allocated memory block.
    /// </summary>
    public int ByteLength => MemoryManager.TotalByteLength;

    /// <summary>
    /// Gets the length in number of elements that this block can hold.
    /// </summary>
    public int Length => MemoryManager.ElementCount;

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
    /// Implicit cast that converts the given <see cref="MemoryBlock{T}"/> to <see cref="Memory{T}"/>.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>
    /// The result of the operation.
    /// </returns>
    public static implicit operator Memory<T>(MemoryBlock<T> block) => block is not null
        ? block.MemoryManager.Memory
        : Memory<T>.Empty;

    /// <summary>
    /// Implicit cast that converts the given <see cref="MemoryBlock{T}"/> to a <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>
    /// The result of the operation.
    /// </returns>
    public static implicit operator Span<T>(MemoryBlock<T> block) => block is not null
        ? block.MemoryManager.GetSpan()
        : [];

    /// <summary>
    /// Converts this instance to a <see cref="Memory{T}"/>.
    /// </summary>
    /// <returns>
    /// The <see cref="Memory"/> of this instance.
    /// </returns>
    public Memory<T> ToMemory() => MemoryManager.Memory;

    /// <summary>
    /// Converts this instance to a <see cref="Span{T}"/>.
    /// </summary>
    /// <returns>
    /// The <see cref="Span"/> of this instance.
    /// </returns>
    public Span<T> ToSpan() => MemoryManager.GetSpan();

    /// <summary>
    /// Returns a handle to the memory that has been pinned and whose address can be taken.
    /// </summary>
    /// <param name="elementIndex">(Optional) Zero-based index of the element.</param>
    /// <remarks>Remember to dispose the returned handle.</remarks>
    /// <returns>
    /// A <see cref="MemoryHandle"/>.
    /// </returns>
    public MemoryHandle Pin(int elementIndex = 0) => MemoryManager.Pin(elementIndex);

    /// <summary>
    /// <seealso cref="Span{T}.Slice(int)"/>
    /// </summary>
    public Memory<T> Slice(int start) => MemoryManager.Memory[start..];

    /// <summary>
    /// <seealso cref="Span{T}.Slice(int, int)"/>
    /// </summary>
    public Memory<T> Slice(int start, int length) => MemoryManager.Memory.Slice(start, length);

    /// <summary>
    /// Clears the contents of the underlying memory.
    /// </summary>
    public void Clear() => MemoryManager.Clear();

    /// <summary>
    /// Reallocates the underlying native memory if the new element count
    /// is greater than <see cref="Length"/>.
    /// </summary>
    /// <param name="elementCount">The length in number of elements that this block can hold.</param>
    public void Reallocate(int elementCount) => MemoryManager.Reallocate(elementCount);

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

        if (MemoryManager is IDisposable memoryManager)
            memoryManager.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
