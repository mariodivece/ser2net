namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// Reads data from a TCP client and sends it to the <see cref="DataBridge.ToPortBuffer"/>.
/// </summary>
internal class NetDataReceiver : BufferWorkerBase<NetDataReceiver>
{
    public NetDataReceiver(ILogger<NetDataReceiver> logger, ServiceSettings settings, DataBridge dataBridge, NetServer server)
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
            if (currentClients.Count <= 0)
            {
                await Task.Delay(1, stoppingToken).ConfigureAwait(false);
                continue;
            }

            foreach (var client in currentClients)
            {
                try
                {
                    var readBuffer = await client.ReadAsync(stoppingToken).ConfigureAwait(false);
                    DataBridge.ToPortBuffer.Enqueue(readBuffer.Span);
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
