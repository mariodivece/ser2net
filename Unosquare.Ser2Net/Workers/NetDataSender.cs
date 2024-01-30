namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from <see cref="DataBridge.ToNetBuffer"/> client and sends it to the TCP connected clients.
/// </summary>
internal sealed class NetDataSender : BufferWorkerBase<NetDataSender>
{
    public NetDataSender(
        ILogger<NetDataSender> logger,
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
            var payload = DataBridge.ToNetBuffer.DequeueAll();

            if (currentClients.Count <= 0 || payload.Length <= 0)
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
                    await client.SendAsync(payload, stoppingToken).ConfigureAwait(false);
                    if (payload.Length > 0)
                    {
                        sample.Record(payload.Length);
                        Logger.ReportStatistics("Network", ConnectionIndex, TransferType.TX, stats, ref lastReportSampleCount);
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
