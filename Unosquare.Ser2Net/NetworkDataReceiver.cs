namespace Unosquare.Ser2Net;

internal class NetworkDataReceiver : BackgroundService
{
    public NetworkDataReceiver(ILogger<NetworkDataSender> logger, NetworkServer server, BufferQueue<byte> bufferQueue)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(bufferQueue);

        Logger = logger;
        Server = server;
        BufferQueue = bufferQueue;
    }

    private ILogger<NetworkDataSender> Logger { get; }

    private NetworkServer Server { get; }

    private BufferQueue<byte> BufferQueue { get; }

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
                    BufferQueue.Enqueue(readBuffer.Span);
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
