namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from all connected TCP clients and sends it to the <see cref="DataBridge.ToPortBuffer"/>.
/// </summary>
internal sealed class NetDataReceiver : BufferWorkerBase<NetDataReceiver>
{
    private long LastReportSampleCount = -1L;

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
                    sample.Record(readBuffer.Length);
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
