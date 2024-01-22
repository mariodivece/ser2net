﻿namespace Unosquare.Ser2Net.Services;

using Unosquare.Ser2Net.Runtime;
using DataQueue = MemoryQueue<byte>;

/// <summary>
/// A data bridge with 2 queues, one to write to the connected serial port,
/// and another one to write to the netowrk client.
/// </summary>
internal sealed class DataBridge
{
    /// <summary>
    /// Gets the buffer of data that will be written to the serial port.
    /// This buffer is filled by the TCP socket.
    /// </summary>
    public DataQueue ToPortBuffer { get; } = new DataQueue(Constants.BridgeQueueSize);

    /// <summary>
    /// Gets the buffer of data that will be written to the TCP socket.
    /// This buffer is filled by the serial port.
    /// </summary>
    public DataQueue ToNetBuffer { get; } = new DataQueue(Constants.BridgeQueueSize);
}
