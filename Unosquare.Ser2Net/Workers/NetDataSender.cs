namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from <see cref="DataBridge.ToNetBuffer"/> client and sends it to the TCP connected clients.
/// </summary>
internal sealed class NetDataSender : BufferWorkerBase<NetDataSender>
{
    private long LastReportSampleCount = -1L;

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
                    sample.Record(payload.Length);
                }
                catch
                {
                    Server.Disconnect(client);
                }
                finally
                {
                    ReportStatistics(stats);
                }
            }
        }
    }

    private void ReportStatistics(StatisticsCollector<int> stats)
    {
        var statCount = stats.LifetimeSampleCount;

        if (statCount <= 0 ||
            statCount == LastReportSampleCount ||
            statCount % Constants.ReportSampleCount != 0)
            return;

        Logger.LogInformation("Data Total: {DataTotal} Data Avg. Rate: {DataRate}",
            stats.LifetimeSamplesSum,
            stats.CurrentNaturalRate);

        LastReportSampleCount = statCount;
    }
}
