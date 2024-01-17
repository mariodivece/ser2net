namespace Unosquare.Ser2Net.Services;

internal class DataBridge
{
    public DataBridge(ILogger<DataBridge> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    private ILogger<DataBridge> Logger { get; }

    public BufferQueue<byte> ToPortBuffer { get; } = new BufferQueue<byte>();

    public BufferQueue<byte> ToNetBuffer { get; } = new BufferQueue<byte>();
}
