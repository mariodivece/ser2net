namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Maintains a serial port connection and acts as a proxy to such serial port.
/// </summary>
internal class SerialPortBroker : BufferWorkerBase<SerialPortBroker>
{
    public SerialPortBroker(ILogger<SerialPortBroker> logger, ServiceSettings settings, DataBridge dataBridge)
        : base(logger, settings, dataBridge)
    {
        // placeholder
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var readBuffer = new MemoryBlock<byte>(4096);
        using var writeBuffer = new MemoryBlock<byte>(4096);

        SerialPort? serialPort = null;

        var performDelay = false;
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
                if (performDelay = !TryConnectSerialPort(out serialPort))
                    continue;

                if (serialPort is null)
                    continue;
            }

            try
            {
                // receive data from serial port
                if (serialPort.BytesToRead > 0)
                {
                    var bytesRead = await serialPort.BaseStream
                        .ReadAsync(readBuffer.Memory, stoppingToken)
                        .ConfigureAwait(false);

                    if (bytesRead > 0)
                        DataBridge.ToNetBuffer.Enqueue(readBuffer[..bytesRead]);

                }

                // send data to serial port
                if (pendingWriteCount > 0)
                {
                    await serialPort.BaseStream
                        .WriteAsync(writeBuffer.Memory[..pendingWriteCount], stoppingToken)
                        .ConfigureAwait(false);
                }

                if (serialPort is not null && serialPort.BytesToRead <= 0 && DataBridge.ToPortBuffer.Count <= 0)
                    await Task.Delay(1, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogInformation("Serial Port {PortName} Disconnected.", serialPort.PortName);
                serialPort.Close();
                serialPort.Dispose();
                serialPort = null;
                
            }
        }

        serialPort?.Dispose();
        Logger.LogInformation("Serial Port Broker Exited");

        // TODO: check if port is in use
        // https://stackoverflow.com/questions/195483/c-sharp-check-if-a-com-serial-port-is-already-open
        //throw new NotImplementedException();
        //readBuffer.Dispose();
        //writeBuffer.Dispose();
    }

    private bool TryConnectSerialPort([MaybeNullWhen(false)] out SerialPort serialPort)
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
            Logger.LogDebug("[Port] Attempting connection on {PortName}.", portName);
            port = new SerialPort(portName,
                Settings.BaudRate, Settings.Parity, Settings.DataBits, Settings.StopBits);

            port.Open();
            Logger.LogInformation("[Port] Connection established on {PortName}.", portName);
            return true;
        }
        catch
        {
            Logger.LogDebug("[Port] Connection on {PortName} failed.", portName);
            return false;
        }
    }
}
