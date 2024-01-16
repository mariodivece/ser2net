namespace Unosquare.Ser2Net.Workers;

internal class NetDataSender : BufferWorkerBase<NetDataSender>
{
    public NetDataSender(ILogger<NetDataSender> logger, ServiceSettings settings, BufferQueue<byte> bufferQueue, NetServer server)
        : base(logger, settings, bufferQueue)
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
