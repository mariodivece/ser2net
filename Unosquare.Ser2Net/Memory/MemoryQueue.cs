namespace Unosquare.Ser2Net.Memory;

/// <summary>
/// Defines a class that represents a generic, resizable circular queue.
/// Ideas initially taken from
/// https://raw.githubusercontent.com/kelindar/circular-buffer/master/Source/ByteQueue.cs
/// but then modernized it and changed it to use native memory for added performance.
/// This class is ideal for byte (or any unmanaged type) buffers as memory
/// is allocated in a contiguous manner and most methods are suitable for
/// <see cref="Span{T}"/> types as opposed to individual elements.
/// </summary>
/// <remarks>
/// This class is thread-safe.
/// It is recommended that you initially
/// </remarks>
public sealed class MemoryQueue<T> : IDisposable
    where T : unmanaged
{
    private const int DefaultInitialCapacity = 1024;

    private readonly int InitialCapacity;
    private readonly int CapacityGrowth;
    private readonly object SyncLock = new();

    private int m_Count;

    /// <summary>
    /// Zero-based index of read operations, AKA the 'Head'.
    /// </summary>
    internal int ReadIndex;

    /// <summary>
    /// Zero-based index of write operations, AKA the 'Tail'
    /// </summary>
    internal int WriteIndex;

    /// <summary>
    /// The region of memory holding the elements.
    /// </summary>
    internal MemoryBlock<T> Buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryQueue{T}"/> class.
    /// </summary>
    public MemoryQueue()
        : this(DefaultInitialCapacity)
    {
        // placeholder
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryQueue{T}"/> class.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the internal element buffer.</param>
    public MemoryQueue(int initialCapacity)
    {
        InitialCapacity = initialCapacity > 0 ? initialCapacity : DefaultInitialCapacity;
        CapacityGrowth = InitialCapacity - 1;
        Buffer = new(initialCapacity);
    }

    /// <summary>
    /// Gest the current capacity of the internal buffer
    /// expressed in number of elements. When at full capacity,
    /// queueing new elements results in automatic growth.
    /// </summary>
    public int Capacity
    {
        get
        {
            lock (SyncLock)
                return Buffer.Length;
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
        private set => m_Count = value;
    }

    /// <summary>
    /// Clears the entire element queue.
    /// </summary>
    public void Clear()
    {
        lock (SyncLock)
        {
            ReadIndex = 0;
            WriteIndex = 0;
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

            ReadIndex = (ReadIndex + elementCount) % Capacity;
            Count -= elementCount;

            if (Count == 0)
            {
                ReadIndex = 0;
                WriteIndex = 0;
            }
        }
    }

    /// <summary>
    /// Adds the provided elements to the queue.
    /// </summary>
    /// <param name="elements">The set coontaining the elements to add.</param>
    public void Enqueue(ReadOnlySpan<T> elements)
    {
        //const int sourceOffset = 0;
        var elementCount = elements.Length;

        if (elementCount <= 0)
            return;

        lock (SyncLock)
        {
            // grow the underlying buffer if there's no more room
            if (Count + elementCount > Capacity)
            {
                var newCapacity = (Count + elementCount + CapacityGrowth) & ~CapacityGrowth;
                Reallocate(newCapacity);
            }

            // compute the number of available slots to write to the right
            // of the write index.
            var rightSlotCount = Capacity - WriteIndex;

            if (ReadIndex >= WriteIndex || rightSlotCount >= elementCount)
            {
                // happy-path copy set of range
                // where we copy all source elements in one go and we have enough
                // room in the target buffer
                var sourceRange = ..elementCount;
                var targetRange = WriteIndex..(WriteIndex + elementCount);
                elements[sourceRange].CopyTo(Buffer[targetRange].Span);
            }
            else
            {
                // Copy to the right buffer slots
                var sourceRange = ..rightSlotCount;
                var targetRange = WriteIndex..(WriteIndex + rightSlotCount);
                elements[sourceRange].CopyTo(Buffer[targetRange].Span);

                // Copy to the left buffer slots
                var leftSlotCount = elementCount - rightSlotCount;
                sourceRange = rightSlotCount..elementCount;
                targetRange = ..leftSlotCount;
                elements[sourceRange].CopyTo(Buffer[targetRange].Span);
            }

            WriteIndex = (WriteIndex + elementCount) % Capacity;
            Count += elementCount;
        }
    }

    /// <summary>
    /// Dequeues the elements into a destination set.
    /// </summary>
    /// <param name="destination">The set in which the dequeued elements will be held.</param>
    /// <returns>The number of elements that were dequeued.</returns>
    public int Dequeue(Span<T> destination) => PeekOrDequeue(destination, doDequeue: true);

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
    public T[] DequeueAll() => PeekOrDequeue(Timeout.Infinite, doDequeue: true);

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

        var elements = PeekOrDequeue(
            elementCount: offsetIndex + 1,
            doDequeue: false);

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
            if (Buffer.IsDisposed) return;
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
        var targetCount = destination.Length;
        
        lock (SyncLock)
        {
            if (targetCount > Count)
                targetCount = Count;

            if (targetCount <= 0)
                return 0;

            // compute the number of slots that can be read from
            // the tight side of the buffer
            var rightSlotCount = Capacity - ReadIndex;

            if (ReadIndex < WriteIndex || rightSlotCount >= targetCount)
            {
                var sourceRange = ReadIndex..(ReadIndex + targetCount);
                var targetRange = ..targetCount;
                Buffer[sourceRange].Span.CopyTo(destination[targetRange]);
            }
            else
            {
                // copy from the right part of the buffer
                var sourceRange = ReadIndex..(ReadIndex + rightSlotCount);
                var targetRange = ..rightSlotCount;
                Buffer[sourceRange].Span.CopyTo(destination[targetRange]);

                // wrap around and copy from the left part of the buffer
                var leftSlotCount = targetCount - rightSlotCount;
                sourceRange = ..leftSlotCount;
                targetRange = rightSlotCount..(rightSlotCount + leftSlotCount);
                Buffer[sourceRange].Span.CopyTo(destination[targetRange]);
            }

            if (doDequeue)
            {
                ReadIndex = (ReadIndex + targetCount) % Capacity;
                Count -= targetCount;

                if (Count == 0)
                {
                    ReadIndex = 0;
                    WriteIndex = 0;
                }
            }

            return targetCount;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reallocate(int newCapacity)
    {
        if (newCapacity <= Capacity)
            return;

        var newBuffer = new MemoryBlock<T>(newCapacity);

        if (Count > 0)
        {
            if (ReadIndex < WriteIndex)
            {
                // we simply copy the surrent counted elements
                var sourceRange = ReadIndex..(ReadIndex + Count);
                var targetRange = ..Count;
                Buffer[sourceRange].CopyTo(newBuffer[targetRange]);
            }
            else
            {
                //Buffer.CopyTo(ReadHead, newBuffer, 0, Capacity - ReadHead);
                var rightSlotCount = Capacity - ReadIndex;
                var sourceRange = ReadIndex..(ReadIndex + rightSlotCount);
                var targetRange = ..rightSlotCount;
                Buffer[sourceRange].CopyTo(newBuffer[targetRange]);

                //Buffer.CopyTo(0, newBuffer, Capacity - ReadHead, WriteTail);
                sourceRange = ..WriteIndex;
                targetRange = rightSlotCount..(rightSlotCount + WriteIndex);
                Buffer[sourceRange].CopyTo(newBuffer[targetRange]);
            }
        }

        ReadIndex = 0;
        WriteIndex = Count;

        // dispose the old buffer
        var oldBuffer = Buffer;
        Buffer = newBuffer;
        oldBuffer.Dispose();
    }
}
