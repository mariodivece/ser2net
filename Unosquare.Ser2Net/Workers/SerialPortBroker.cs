namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Maintains a serial port connection and acts as a proxy to such serial port.
/// </summary>
internal class SerialPortBroker(ILogger<SerialPortBroker> logger, ServiceSettings settings, DataBridge dataBridge) :
    BufferWorkerBase<SerialPortBroker>(logger, settings, dataBridge)
{
    const string LoggerName = "Serial";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var readBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);
        using var writeBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);
        SerialPort? serialPort = null;
        var performDelay = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // always dequeue regardless of connection state
                var pendingWriteCount = DataBridge.ToPortBuffer.Dequeue(writeBuffer.Span);

                if (performDelay)
                {
                    await Task.Delay(500, stoppingToken).ConfigureAwait(false);
                    performDelay = false;
                    continue;
                }

                if (serialPort is null)
                {
                    if (performDelay = !TryConnectWantedPort(out serialPort))
                        continue;

                    if (serialPort is null)
                        continue;
                }

                try
                {
                    // fire up the receive task
                    var receiveTask = ReceiveSerialPortDataAsync(serialPort, readBuffer.Memory, stoppingToken);

                    // send data to serial port
                    if (pendingWriteCount > 0)
                    {
                        await serialPort.BaseStream
                            .WriteAsync(writeBuffer.Memory[..pendingWriteCount], stoppingToken)
                            .ConfigureAwait(false);
                    }

                    // wait for the receive task to complete also
                    await receiveTask.ConfigureAwait(false);

                    // give the task a break if there's nothing to do at this point
                    if (serialPort is not null && serialPort.BytesToRead <= 0 && DataBridge.ToPortBuffer.Count <= 0)
                        await Task.Delay(1, stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    Logger.LogPortDisconnected(LoggerName, serialPort.PortName);
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                }
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            serialPort?.Dispose();
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

    private bool TrySerialPortConnection(string portName, [MaybeNullWhen(false)] out SerialPort? port)
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
