namespace Unosquare.Ser2Net;

internal class NetworkDataSender : BackgroundService
{
    public NetworkDataSender(ILogger<NetworkDataSender> logger, NetworkServer server, BufferQueue<byte> bufferQueue)
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

            if (BufferQueue.Count < 20 || currentClients.Count <= 0)
            {
                await Task.Delay(1, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var echoBytes = BufferQueue.DequeueAll();

            foreach (var client in currentClients)
            {
                try
                {
                    await client.WriteAsync(
                        Encoding.UTF8.GetBytes("\r\nReceived 20 bytes. Will spit them out and wait for more.\r\n"), stoppingToken)
                    .ConfigureAwait(false);
                    await client.WriteAsync(echoBytes, stoppingToken).ConfigureAwait(false);
                    await client.WriteAsync(
                        Encoding.UTF8.GetBytes($"\r\n\tQ: Capacity = {BufferQueue.Capacity}, Count = {BufferQueue.Count}\r\n\r\n"), stoppingToken)
                    .ConfigureAwait(false);
                }
                catch
                {
                    Server.Disconnect(client);
                }
            }
        }
    }
}
