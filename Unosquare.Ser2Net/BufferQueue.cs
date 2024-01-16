using System.Runtime.CompilerServices;

namespace Unosquare.Ser2Net;

/// <summary>
/// Defines a class that represents a generic resizable circular queue.
/// Initially taken from
/// https://raw.githubusercontent.com/kelindar/circular-buffer/master/Source/ByteQueue.cs
/// This class is ideal for byte (or any unmanaged type) buffers as memory
/// is allocated in a contiguous manner and most methods are suitable for
/// <see cref="Span{T}"/> types as opposed to individual elements.
/// </summary>
/// <remarks>This class is thread-safe.</remarks>
public sealed class BufferQueue<T> : IDisposable
    where T : unmanaged
{
    private const int DefaultInitialCapacity = 2048;

    private readonly int InitialCapacity;
    private readonly int CapacityGrowth;
    private readonly object SyncLock = new();

    private bool m_IsDisposed;
    private int m_Count;
    private int ReadHead;
    private int WriteTail;
    private IMemoryOwner<T> Buffer;
    private MemoryHandle BufferHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferQueue{T}"/> class.
    /// </summary>
    public BufferQueue()
        : this(DefaultInitialCapacity)
    {
        // placeholder
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferQueue{T}"/> class.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the internal element buffer.</param>
    public BufferQueue(int initialCapacity)
    {
        InitialCapacity = initialCapacity > 0 ? initialCapacity : DefaultInitialCapacity;
        CapacityGrowth = InitialCapacity - 1;
        Buffer = MemoryPool<T>.Shared.Rent(initialCapacity);
        BufferHandle = Buffer.Memory.Pin();
    }

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (SyncLock)
                return m_IsDisposed;
        }
    }

    /// <summary>
    /// Gest the current capacity of the internal buffer
    /// expressed in number of elements.
    /// </summary>
    public int Capacity
    {
        get
        {
            lock (SyncLock)
                return Buffer.Memory.Length;
        }
    }

    /// <summary>
    /// Gets number of elements that can currently be read off the queue.
    /// </summary>
    public int Count
    {
        get
        {
            lock (SyncLock)
                return m_Count;
        }
        private set
        {
            lock (SyncLock)
                m_Count = value;
        }
    }

    /// <summary>
    /// Clears the entire element queue.
    /// </summary>
    public void Clear()
    {
        lock (SyncLock)
        {
            ReadHead = 0;
            WriteTail = 0;
            Count = 0;
        }
    }

    /// <summary>
    /// Clears the specified number of elements from the queue.
    /// </summary>
    public void Clear(int elementCount)
    {
        lock (SyncLock)
        {
            if (elementCount > Count)
                elementCount = Count;

            if (elementCount <= 0)
                return;

            ReadHead = (ReadHead + elementCount) % Capacity;
            Count -= elementCount;

            if (Count == 0)
            {
                ReadHead = 0;
                WriteTail = 0;
            }
        }
    }

    /// <summary>
    /// Adds the provided elements to the queue.
    /// </summary>
    /// <param name="elements">The set coontaining the elements to add.</param>
    public void Enqueue(ReadOnlySpan<T> elements)
    {
        const int sourceOffset = 0;
        var inputCount = elements.Length;

        if (inputCount <= 0)
            return;

        lock (SyncLock)
        {
            if ((Count + inputCount) > Capacity)
                Reallocate((Count + inputCount + CapacityGrowth) & ~CapacityGrowth);

            if (ReadHead < WriteTail)
            {
                int rightLength = Capacity - WriteTail;

                if (rightLength >= inputCount)
                {
                    BlockCopy(elements, sourceOffset, Buffer, WriteTail, inputCount);
                }
                else
                {
                    BlockCopy(elements, sourceOffset, Buffer, WriteTail, rightLength);
                    BlockCopy(elements, sourceOffset + rightLength, Buffer, 0, inputCount - rightLength);
                }
            }
            else
            {
                BlockCopy(elements, sourceOffset, Buffer, WriteTail, inputCount);
            }

            WriteTail = (WriteTail + inputCount) % Capacity;
            Count += inputCount;
        }
    }

    /// <summary>
    /// Adds the provided elements to the queue.
    /// </summary>
    /// <param name="elements">The set coontaining the elements to add.</param>
    /// <param name="startIndex">The start index of the elements being added.</param>
    public void Enqueue(ReadOnlySpan<T> elements, int startIndex) => Enqueue(elements[startIndex..]);

    /// <summary>
    /// Adds the provided elements to the queue.
    /// </summary>
    /// <param name="elements">The set coontaining the elements to add.</param>
    /// <param name="startIndex">The start index of the elements being added.</param>
    /// <param name="count">The maximum number of elements to add.</param>
    public void Enqueue(ReadOnlySpan<T> elements, int startIndex, int count) => Enqueue(elements.Slice(startIndex, count));

    /// <summary>
    /// Dequeues the elements into a destination set.
    /// </summary>
    /// <param name="destination">The set in which the dequeued elements will be held.</param>
    /// <returns>The number of elements that were dequeued.</returns>
    public int Dequeue(Span<T> destination) => PeekOrDequeue(destination, doDequeue: true);

    /// <summary>
    /// Dequeues the elements into a destination set.
    /// </summary>
    /// <param name="destination">The set in which the dequeued elements will be held.</param>
    /// <param name="startIndex">The start index at which the destination is written with dequeued elements.</param>
    /// <returns>The number of elements that were dequeued.</returns>
    public int Dequeue(Span<T> destination, int startIndex) => Dequeue(destination[startIndex..]);

    /// <summary>
    /// Dequeues the elements into a destination set.
    /// </summary>
    /// <param name="destination">The set in which the dequeued elements will be held.</param>
    /// <param name="startIndex">The start index at which the destination is written with dequeued elements.</param>
    /// <param name="count">The maximum number of elements to dequeue.</param>
    /// <returns>The number of elements that were dequeued.</returns>
    public int Dequeue(Span<T> destination, int startIndex, int count) => Dequeue(destination.Slice(startIndex, count));

    /// <summary>
    /// Dequeues the specified number of elements off the queue.
    /// </summary>
    /// <param name="elementCount">The maximum number of elements to dequeue.</param>
    /// <returns>The dequeued elements.</returns>
    public T[] Dequeue(int elementCount) => PeekOrDequeue(elementCount, doDequeue: true);

    /// <summary>
    /// Dequeues all the available elements off the queue.
    /// </summary>
    /// <returns>The dequeued elements.</returns>
    public T[] Dequeue() => Dequeue(Timeout.Infinite);

    /// <summary>
    /// Attempts to retrieve an element, without removing it from the queue,
    /// at the relative offset index from the element currently at the head of the queue.
    /// For example, if the offset index provided is 0, the element
    /// at the head of the queue is returned. If the offset index provided
    /// is 1, then the element at the head of the queue is skipped and the
    /// following element is returned, and so on.
    /// </summary>
    /// <param name="offsetIndex">An offset index from the element at the head of the queue.</param>
    /// <param name="element">The element that was retrieved if the operation is successful.</param>
    /// <returns>True if the element was retrieved, and false otherwise.</returns>
    public bool TryPeek(int offsetIndex, out T element)
    {
        element = default;

        if (offsetIndex < 0)
            offsetIndex = 0;

        var elements = Peek(elementCount: offsetIndex + 1);
        if (offsetIndex < elements.Length)
        {
            element = elements[offsetIndex];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve the next available element in the queue.
    /// That is, the element at the head of the queue.
    /// </summary>
    /// <param name="element">The element that was retrieved if the operation is successful.</param>
    /// <returns>True if the element was retrieved, and false otherwise.</returns>
    public bool TryPeek(out T element) => TryPeek(0, out element);

    /// <summary>
    /// Retrieves the specified number of elements without dequeueing them.
    /// </summary>
    /// <param name="elementCount">The maximum number of elements to retrieve.</param>
    /// <returns>The retrieved elements.</returns>
    public T[] Peek(int elementCount) => PeekOrDequeue(elementCount, doDequeue: false);

    /// <summary>
    /// Retrieves elements into the specified destination without dequeueing them.
    /// </summary>
    /// <param name="destination">The set to which the elements will be written.</param>
    /// <returns>The number of elements that were retrieved.</returns>
    public int Peek(Span<T> destination) => PeekOrDequeue(destination, doDequeue: false);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (SyncLock)
        {
            if (m_IsDisposed) return;

            m_IsDisposed = true;
            BufferHandle.Dispose();
            Buffer.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T[] PeekOrDequeue(int elementCount, bool doDequeue)
    {
        if (elementCount < 0)
            elementCount = Count;

        elementCount = Math.Min(elementCount, Count);
        Span<T> output = stackalloc T[elementCount];
        elementCount = PeekOrDequeue(output, doDequeue);
        return output[..elementCount].ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PeekOrDequeue(Span<T> destination, bool doDequeue)
    {
        const int targetOffset = 0;
        var targetCount = destination.Length;

        lock (SyncLock)
        {
            if (targetCount > Count)
                targetCount = Count;

            if (targetCount <= 0)
                return 0;

            if (ReadHead < WriteTail)
            {
                BlockCopy(Buffer, ReadHead, destination, targetOffset, targetCount);
            }
            else
            {
                var rightLength = Capacity - ReadHead;

                if (rightLength >= targetCount)
                {
                    BlockCopy(Buffer, ReadHead, destination, targetOffset, targetCount);
                }
                else
                {
                    BlockCopy(Buffer, ReadHead, destination, targetOffset, rightLength);
                    BlockCopy(Buffer, 0, destination, targetOffset + rightLength, targetCount - rightLength);
                }
            }

            if (doDequeue)
            {
                ReadHead = (ReadHead + targetCount) % Capacity;
                Count -= targetCount;

                if (Count == 0)
                {
                    ReadHead = 0;
                    WriteTail = 0;
                }
            }

            return targetCount;
        }
    }

    #region Memory Management

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reallocate(int newCapacity)
    {
        if (newCapacity <= Capacity)
            return;

        var newBuffer = MemoryPool<T>.Shared.Rent(newCapacity);

        if (Count > 0)
        {
            if (ReadHead < WriteTail)
            {
                BlockCopy(Buffer, ReadHead, newBuffer, 0, Count);
            }
            else
            {
                BlockCopy(Buffer, ReadHead, newBuffer, 0, Capacity - ReadHead);
                BlockCopy(Buffer, 0, newBuffer, Capacity - ReadHead, WriteTail);
            }
        }

        ReadHead = 0;
        WriteTail = Count;

        // dispose the old buffer and memory handle
        BufferHandle.Dispose();
        Buffer.Dispose();

        // Set the internal buffer to the newly allocated one
        Buffer = newBuffer;
        BufferHandle = Buffer.Memory.Pin();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BlockCopy(ReadOnlySpan<T> source, int sourceOffset, Span<T> destination, int destinationOffset, int count)
    {
        var maxSourceCount = source.Length - sourceOffset;
        var maxDestinationCount = destination.Length - destinationOffset;
        var maxCount = Math.Min(maxSourceCount, maxDestinationCount);

        if (count > maxCount)
            count = maxCount;

        if (count > 0 &&
            source.Slice(sourceOffset, count).TryCopyTo(
            destination.Slice(destinationOffset, count)))
        {
            return count;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BlockCopy(IMemoryOwner<T> source, int sourceOffset, Span<T> destination, int destinationOffset, int count) =>
        BlockCopy(source.Memory.Span, sourceOffset, destination, destinationOffset, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BlockCopy(IMemoryOwner<T> source, int sourceOffset, IMemoryOwner<T> destination, int destinationOffset, int count) =>
        BlockCopy(source.Memory.Span, sourceOffset, destination.Memory.Span, destinationOffset, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BlockCopy(ReadOnlySpan<T> source, int sourceOffset, IMemoryOwner<T> destination, int destinationOffset, int count) =>
        BlockCopy(source, sourceOffset, destination.Memory.Span, destinationOffset, count);

    #endregion
}
