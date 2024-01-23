namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from <see cref="DataBridge.ToNetBuffer"/> client and sends it to the TCP connected clients.
/// </summary>
internal sealed class NetDataSender : BufferWorkerBase<NetDataSender>
{
    public NetDataSender(ILogger<NetDataSender> logger, ConnectionSettingsItem settings, DataBridge dataBridge, NetServer server)
        : base(logger, settings, dataBridge)
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
            var echoBytes = DataBridge.ToNetBuffer.DequeueAll();

            if (currentClients.Count <= 0 || echoBytes.Length <= 0)
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
                    await client.SendAsync(echoBytes, stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
