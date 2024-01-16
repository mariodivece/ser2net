namespace Unosquare.Ser2Net.Workers;

internal abstract class BufferWorkerBase<T> : WorkerBase<T>
    where T : BackgroundService
{
    public BufferWorkerBase(ILogger<T> logger, ServiceSettings settings, BufferQueue<byte> bufferQueue)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(bufferQueue);

        BufferQueue = bufferQueue;
    }

    protected BufferQueue<byte> BufferQueue { get; }
}
