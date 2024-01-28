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
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentClients = Server.Clients;
            var payload = DataBridge.ToNetBuffer.DequeueAll();

            if (currentClients.Count <= 0 || payload.Length <= 0)
            {
                // prevent exceptions on task delays
                try { await Task.Delay(1, stoppingToken).ConfigureAwait(false); }
                catch { }
                continue;
            }

            foreach (var client in currentClients)
            {
                try
                {
                    await client.SendAsync(payload, stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
