using Unosquare.Ser2Net.Memory;

namespace Unosquare.Ser2Net.Services;

internal class DataBridge
{
    public DataBridge(ILogger<DataBridge> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    private ILogger<DataBridge> Logger { get; }

    public MemoryQueue<byte> ToPortBuffer { get; } = new MemoryQueue<byte>();

    public MemoryQueue<byte> ToNetBuffer { get; } = new MemoryQueue<byte>();
}
