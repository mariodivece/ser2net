using System.Collections.Concurrent;

namespace Unosquare.Ser2Net;

/// <summary>
/// A serial port server.
/// </summary>
internal sealed class SerialPortServer : BackgroundService
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortServer"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The environment.</param>
    public SerialPortServer(
        ILogger<SerialPortServer> logger,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
        : base()
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(configuration);

        Logger = logger;
        Lifetime = lifetime;
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    public IHostApplicationLifetime Lifetime { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public ILogger<SerialPortServer> Logger { get; }

    public TcpListener? TcpServer { get; private set; }

    public ServiceSettings Settings { get; } = new();

    private List<SerialPortClient> Clients { get; } = [];

    private void DisconnectClients()
    {
        for (var i = Clients.Count - 1; i >= 0; i--)
            Clients[i].Dispose();

        Clients.Clear();
    }

    private void Dispose(bool alsoManaged)
    {
        if (isDisposed) return;
        isDisposed = true;

        if (alsoManaged)
        {
            DisconnectClients();
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }

    public async Task ListenForNetworkClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serverAddress = ParseServerAddress();
            var serverEndpoint = new IPEndPoint(serverAddress, Settings.ServerPort);
            TcpServer = new TcpListener(serverEndpoint);
            TcpServer.Start();

            Logger.LogInformation("Listening for TCP clients on {EndPoint}", serverEndpoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await SerialPortClient.WaitForClientAsync(this, cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("Client {EndPoint} Connection accepted.", client.RemoteEndPoint);

                    if (Clients.Count >= Constants.MaxClientCount)
                    {
                        Logger.LogWarning("Client [{EndPoint}] rejected because connection count would exceed {MaxConnections}.",
                            client.RemoteEndPoint, Constants.MaxClientCount);

                        client.Dispose();
                        client = null;
                        continue;
                    }

                    if (!client.IsConnected)
                    {
                        Logger.LogWarning("Client [{EndPoint}] did not complete the connection",
                            client.RemoteEndPoint);

                        client.Dispose();
                        continue;
                    }

                    Clients.Add(client);
                }
                catch (Exception ex)
                {

                }
            }
        }
        catch (Exception ex)
        {

        }
        finally
        {
            Logger.LogInformation("TCP Listener [{EndPoint}] Shutting down . . . ", TcpServer?.LocalEndpoint);
            TcpServer?.Stop();
            TcpServer?.Dispose();
            TcpServer = null;

        }
    }

    public async Task RunEchoServer(CancellationToken cancellation)
    {
        var byteQueue = new BufferQueue<byte>();
        var clientsSyncRoot = new SemaphoreSlim(1, 1);
        var disconnectedClients = new ConcurrentQueue<SerialPortClient>();

        async Task removeDisconnectedClients()
        {
            try
            {
                await clientsSyncRoot.WaitAsync(cancellation).ConfigureAwait(false);

                if (disconnectedClients.IsEmpty)
                    return;

                while (!disconnectedClients.IsEmpty)
                {
                    if (!disconnectedClients.TryDequeue(out var client) || client is null)
                        continue;

                    client.Dispose();
                    Clients.Remove(client);
                }
            }
            finally
            {
                clientsSyncRoot.Release();
            }
        }
        async Task<SerialPortClient[]> getCurrentClients()
        {
            try
            {
                await clientsSyncRoot.WaitAsync(cancellation).ConfigureAwait(false);
                return [.. Clients];
            }
            finally
            {
                clientsSyncRoot.Release();
            }
        }

        var readTask = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                var currentClients = await getCurrentClients().ConfigureAwait(false);
                if (currentClients.Length <= 0)
                {
                    await Task.Delay(1, cancellation).ConfigureAwait(false);
                    continue;
                }

                foreach (var client in currentClients)
                {
                    try
                    {
                        var readBuffer = await client.ReadAsync(cancellation).ConfigureAwait(false);
                        byteQueue.Enqueue(readBuffer.Span);
                    }
                    catch
                    {
                        disconnectedClients.Enqueue(client);
                    }
                }

                await removeDisconnectedClients().ConfigureAwait(false);
            }
        });

        var writeTask = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (byteQueue.Count < 20)
                {
                    await Task.Delay(1, cancellation).ConfigureAwait(false);
                    continue;
                }

                var echoBytes = byteQueue.Dequeue();

                var currentClients = await getCurrentClients().ConfigureAwait(false);
                foreach (var client in currentClients)
                {
                    try
                    {
                        await client.WriteAsync(
                            Encoding.UTF8.GetBytes("\r\nReceived 20 bytes. Will spit them out and wait for more.\r\n"), cancellation)
                        .ConfigureAwait(false);
                        await client.WriteAsync(echoBytes, cancellation).ConfigureAwait(false);
                        await client.WriteAsync(
                            Encoding.UTF8.GetBytes($"\r\n\tQ: Capacity = {byteQueue.Capacity}, Count = {byteQueue.Count}\r\n\r\n"), cancellation)
                        .ConfigureAwait(false);
                    }
                    catch
                    {
                        disconnectedClients.Enqueue(client);
                    }
                }

                await removeDisconnectedClients().ConfigureAwait(false);

                if (currentClients.Length <= 0)
                    await Task.Delay(1, cancellation).ConfigureAwait(false);
            }
        });

        try
        {
            await Task.WhenAll(readTask, writeTask).ConfigureAwait(false);
        }
        finally
        {
            byteQueue.Dispose();
            clientsSyncRoot.Dispose();
            DisconnectClients();
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogServiceStarting(nameof(SerialPortServer));

        try
        {
            ReadSettings();
            var tasks = new List<Task>
            {
                ListenForNetworkClientsAsync(stoppingToken),
                RunEchoServer(stoppingToken),
            };
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Environment.ExitCode = Constants.ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            Environment.ExitCode = Constants.ExitCodeFailure;
        }
        finally
        {
            Lifetime.StopApplication();
        }
    }

    private void ReadSettings()
    {
        Configuration.GetRequiredSection(ServiceSettings.SectionName).Bind(Settings);
    }

    private IPAddress ParseServerAddress()
    {
        if (!IPAddress.TryParse(Settings.ServerIP, out var serverIP))
        {
            Logger.LogWarning("Settings Server IP '{ServerIP}' is invalid. Will use all available local addresses.", Settings.ServerIP);
            serverIP = Constants.DefaultServerIP;
        }
        else
        {
            Logger.LogInformation("Server IP: {ServerIP}", Settings.ServerIP);
        }

        return serverIP;
    }
}
