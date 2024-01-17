namespace Unosquare.Ser2Net.Workers;

internal abstract class BufferWorkerBase<T> : WorkerBase<T>
    where T : BackgroundService
{
    public BufferWorkerBase(ILogger<T> logger, ServiceSettings settings, DataBridge dataBridge)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(dataBridge);
        DataBridge = dataBridge;
    }

    protected DataBridge DataBridge { get; }
}
