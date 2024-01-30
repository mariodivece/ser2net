﻿using System.Xml;

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
    private SerialPort? Port;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var readBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);
        using var writeBuffer = new MemoryBlock<byte>(Constants.DefaultBlockSize);

        using var readStats = new StatisticsCollector<int>(true);
        using var writeStats = new StatisticsCollector<int>(true);

        var reportSampleCount = 50L;
        var lastReportCount = -1L;
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
                    var receiveTask = ReceiveSerialPortDataAsync(
                        Port, readBuffer, DataBridge, readStats, stoppingToken)
                        .ConfigureAwait(false);

                    // fire up the send task
                    var sendTask = SendSerialPortDataAsync(
                        Port, writeBuffer[..pendingWriteLength], writeStats, stoppingToken)
                        .ConfigureAwait(false);

                    // await the tasks
                    await receiveTask;
                    await sendTask;

                    var statCount = readStats.LifetimeSampleCount + writeStats.LifetimeSampleCount;

                    if (statCount != lastReportCount && statCount % reportSampleCount == 0)
                    {
                        Logger.LogInformation("TX Total: {TxTotal} TX Avg. Rate: {TxRate} RX Total: {RxTotal} RX Avg. Rate {RxRate}",
                            writeStats.LifetimeSamplesSum,
                            writeStats.CurrentRatesAverage,
                            readStats.LifetimeSamplesSum,
                            readStats.CurrentRatesAverage);

                        lastReportCount = statCount;
                    }

                    // give the task a break if there's nothing to do at this point
                    if (Port is not null && Port.BytesToRead <= 0 && DataBridge.ToPortBuffer.Length <= 0)
                        await Task.Delay(Constants.ShortDelayMilliseconds, stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    Logger.LogPortDisconnected(ConnectionIndex, Port?.PortName ?? "NOPORT");
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
            Logger.LogBrokerStopped(ConnectionIndex);
        }
    }

    private static async ValueTask<int> ReceiveSerialPortDataAsync(
        SerialPort? currentPort, Memory<byte> readMemory, DataBridge bridge, StatisticsCollector<int> stats, CancellationToken token)
    {
        using var sample = stats.Begin();
        var bytesRead = 0;

        try
        {
            if (currentPort is null ||
                currentPort.BytesToRead <= 0 ||
                !currentPort.IsOpen ||
                currentPort.BreakState ||
                token.IsCancellationRequested)
                return 0;

            bytesRead = await currentPort.BaseStream
                .ReadAsync(readMemory, token)
                .ConfigureAwait(false);

            if (bytesRead > 0)
                bridge.ToNetBuffer.Enqueue(readMemory.Span[..bytesRead]);

            return bytesRead;
        }
        finally
        {
            sample.Record(bytesRead);
        }
    }

    private static async ValueTask<int> SendSerialPortDataAsync(
        SerialPort? currentPort, Memory<byte> writeMemory, StatisticsCollector<int> stats, CancellationToken token)
    {
        using var sample = stats.Begin();
        var bytesWritten = 0;

        try
        {
            if (writeMemory.IsEmpty ||
                currentPort is null ||
                !currentPort.IsOpen ||
                currentPort.BreakState ||
                token.IsCancellationRequested)
                return 0;

            await currentPort.BaseStream
                .WriteAsync(writeMemory, token)
                .ConfigureAwait(false);

            bytesWritten = writeMemory.Length;
            return bytesWritten;
        }
        finally
        {
            sample.Record(bytesWritten);
        }
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
            Logger.LogAttemptingConnection(ConnectionIndex, portName);
            port = new SerialPort(portName,
                Settings.BaudRate, Settings.Parity, Settings.DataBits, Settings.StopBits);

            port.Open();
            Logger.LogPortConnected(ConnectionIndex, portName);
            return true;
        }
        catch
        {
            Logger.LogConnectionFailed(ConnectionIndex, portName);
            return false;
        }
    }
}
