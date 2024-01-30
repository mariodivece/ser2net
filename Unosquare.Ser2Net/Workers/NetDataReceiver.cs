namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from all connected TCP clients and sends it to the <see cref="DataBridge.ToPortBuffer"/>.
/// </summary>
internal sealed class NetDataReceiver : BufferWorkerBase<NetDataReceiver>
{
    public NetDataReceiver(
        ILogger<NetDataReceiver> logger,
        NetServer server,
        DataBridge dataBridge)
        : base(logger, server.Settings, dataBridge)
    {
        ArgumentNullException.ThrowIfNull(server);
        Server = server;
    }

    private NetServer Server { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastReportSampleCount = -1L;
        using var stats = new StatisticsCollector<int>(ignoreZeroes: true);

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentClients = Server.Clients;
            if (currentClients.Count <= 0)
            {
                // prevent exceptions on task delays
                try { await Task.Delay(Constants.ShortDelayMilliseconds, stoppingToken).ConfigureAwait(false); }
                catch { }
                continue;
            }

            foreach (var client in currentClients)
            {
                try
                {
                    using var sample = stats.BeginSample();
                    var readBuffer = await client.ReceiveAsync(stoppingToken).ConfigureAwait(false);
                    DataBridge.ToPortBuffer.Enqueue(readBuffer.Span);
                    if (readBuffer.Length > 0)
                    {
                        sample.Record(readBuffer.Length);
                        Logger.ReportStatistics("Network", ConnectionIndex, TransferType.RX, stats, ref lastReportSampleCount);
                    }
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
