namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Maintains a serial port connection and acts as a proxy to such serial port.
/// </summary>
internal sealed class SerialPortBroker(
    ILogger<SerialPortBroker> logger,
    ConnectionSettingsItem settings,
    DataBridge dataBridge) :
    BufferWorkerBase<SerialPortBroker>(logger, settings, dataBridge)
{
    const string LoggerName = "Serial";
    private SerialPort? Port;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var readBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);
        using var writeBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // always dequeue regardless of connection state
                // because we don't want to fill up the queue with
                // data when there is potentially no serial port connected
                var pendingWriteLength = DataBridge.ToPortBuffer.Dequeue(writeBuffer);

                // Attempt serial port connection if one is not active.
                if (Port is null && !TryConnectWantedPort(out Port))
                {
                    // give the loop a break. Don't go as fast as possible
                    // retrying serial port connection so we don't peg a core
                    // unnecessarily. Don't make the delay too long because we might
                    // still have data being queued up.
                    Port?.Dispose();
                    await Task.Delay(Constants.LongDelayMillisconds, stoppingToken)
                        .ConfigureAwait(false);

                    continue;
                }

                try
                {
                    // fire up the receive task
                    var receiveTask = ReceiveSerialPortDataAsync(Port, readBuffer, stoppingToken);

                    // send data to serial port
                    if (pendingWriteLength > 0)
                    {
                        await Port.BaseStream
                            .WriteAsync(writeBuffer[..pendingWriteLength], stoppingToken)
                            .ConfigureAwait(false);
                    }

                    // wait for the receive task to complete also
                    await receiveTask.ConfigureAwait(false);

                    // give the task a break if there's nothing to do at this point
                    if (Port is not null && Port.BytesToRead <= 0 && DataBridge.ToPortBuffer.Count <= 0)
                        await Task.Delay(1, stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    Logger.LogPortDisconnected(LoggerName, Port?.PortName ?? "NOPORT");
                    Port?.Close();
                    Port?.Dispose();
                    Port = null;
                }
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            Port?.Dispose();
            Logger.LogBrokerStopped(LoggerName);
        }
    }

    private async ValueTask ReceiveSerialPortDataAsync(
        SerialPort? currentPort, Memory<byte> readMemory, CancellationToken token)
    {
        if (currentPort is null ||
            currentPort.BytesToRead <= 0 ||
            !currentPort.IsOpen ||
            currentPort.BreakState ||
            token.IsCancellationRequested)
            return;

        var bytesRead = await currentPort.BaseStream
            .ReadAsync(readMemory, token)
            .ConfigureAwait(false);

        if (bytesRead > 0)
            DataBridge.ToNetBuffer.Enqueue(readMemory.Span[..bytesRead]);
    }

    private bool TryConnectWantedPort([MaybeNullWhen(false)] out SerialPort serialPort)
    {
        serialPort = null;
        var serialPortNames = SerialPort.GetPortNames();

        // No serial ports detected so far
        if (serialPortNames.Length <= 0)
            return false;

        var wantedPortNames = GetWantedPortNames(serialPortNames);

        // No matching port name found
        if (wantedPortNames.Length <= 0)
            return false;

        // attempt connections
        foreach (var portName in wantedPortNames)
        {
            if (TrySerialPortConnection(portName, out var wantedSerialPort) &&
                wantedSerialPort is not null)
            {
                serialPort = wantedSerialPort;
                break;
            }
        }

        return serialPort is not null;
    }

    private string[] GetWantedPortNames(string[] serialPortNames)
    {
        if (string.IsNullOrWhiteSpace(Settings.PortName))
            return serialPortNames;

        var wantedPortNames = new List<string>(serialPortNames.Length);
        var wantedPortName = Settings.PortName.Trim();
        foreach (var portName in serialPortNames)
        {
            if (portName.Equals(wantedPortName, StringComparison.OrdinalIgnoreCase))
            {
                wantedPortNames.Add(portName);
                break;
            }
        }

        return [.. wantedPortNames];
    }

    private bool TrySerialPortConnection(string portName, [NotNullWhen(true)] out SerialPort? port)
    {
        port = null;

        try
        {
            Logger.LogAttemptingConnection(LoggerName, portName);
            port = new SerialPort(portName,
                Settings.BaudRate, Settings.Parity, Settings.DataBits, Settings.StopBits);

            port.Open();
            Logger.LogPortConnected(LoggerName, portName);
            return true;
        }
        catch
        {
            Logger.LogConnectionFailed(LoggerName, portName);
            return false;
        }
    }
}
