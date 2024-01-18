namespace Unosquare.Ser2Net.Services;

/// <summary>
/// A data bridge with 2 queues, one to write to the connected serial port,
/// and another one to write to the netowrk client.
/// </summary>
internal class DataBridge
{
    /// <summary>
    /// Gets the buffer of data that will be written to the serial port.
    /// This buffer is filled by the TCP socket.
    /// </summary>
    public MemoryQueue<byte> ToPortBuffer { get; } = new MemoryQueue<byte>(1);

    /// <summary>
    /// Gets the buffer of data that will be written to the TCP socket.
    /// This buffer is filled by the serial port.
    /// </summary>
    public MemoryQueue<byte> ToNetBuffer { get; } = new MemoryQueue<byte>(1);
}
