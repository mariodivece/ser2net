namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// A TCP listener waiting for connections on a specified local endpoint.
/// This class cannot be inherited.
/// </summary>
internal sealed class NetServer
    : ConnectionWorkerBase<NetServer>
{
    private int LastClientId;
    private readonly ConcurrentDictionary<int, NetworkClient> m_Clients = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NetServer"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="logger">The logger.</param>
    /// <param name="services">Gets the DI container services provider.</param>
    /// <param name="settings">Options for controlling the operation.</param>
    public NetServer(
        ILogger<NetServer> logger,
        IServiceProvider services,
        ConnectionSettingsItem settings)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Gets the DI container services provider.
    /// </summary>
    private IServiceProvider Services { get; }

    /// <summary>
    /// Gets a snapshot of the connected clients.
    /// The underlying client collection may change
    /// after the snapshot is taken.
    /// </summary>
    public IReadOnlyList<NetworkClient> Clients => m_Clients.Values.ToArray();


    /// <summary>
    /// Disconnects the given client and removes it from the underlying client list.
    /// </summary>
    /// <param name="client">The client to disconnect.</param>
    public void Disconnect(NetworkClient client)
    {
        if (client is null)
            return;

        var keys = m_Clients.Keys.ToArray();
        foreach (var key in keys)
        {
            if (Equals(m_Clients[key], client) &&
                m_Clients.TryRemove(key, out _))
            {
                client.Dispose();
                break;
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TcpListener? tcpServer = default;

        try
        {
            tcpServer = new TcpListener(Settings.ServerIP, Settings.ServerPort);
            tcpServer.Start();

            Logger.LogListenerStarted(ConnectionIndex, tcpServer.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await tcpServer.AcceptSocketAsync(stoppingToken).ConfigureAwait(false);
                    var client = Services.CreateInstance<NetworkClient>(Settings, socket);

                    Logger.LogClientAccepted(ConnectionIndex, client.RemoteEndPoint);

                    if (m_Clients.Count >= Constants.MaxClientCount)
                    {
                        Logger.LogConnectionRejectedMax(ConnectionIndex, client.RemoteEndPoint, Constants.MaxClientCount);
                        client.Dispose();
                        client = null;
                        continue;
                    }

                    if (!client.IsConnected)
                    {
                        Logger.LogConnectionNotCompleted(ConnectionIndex, client.RemoteEndPoint);
                        client.Dispose();
                        continue;
                    }

                    m_Clients[Interlocked.Increment(ref LastClientId)] = client;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation was requested
                    // do not throw exception
                    break;
                }
                catch (Exception ex)
                {
                    // unhandled exception
                    Logger.LogListenerLoopFailed(ConnectionIndex, tcpServer?.LocalEndpoint, ex.Message, ex);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogListenerFailed(ConnectionIndex, tcpServer?.LocalEndpoint, ex.Message, ex);
            throw;
        }
        finally
        {
            Logger.LogListenerShuttingDown(ConnectionIndex, tcpServer?.LocalEndpoint);
            tcpServer?.Stop();
            tcpServer?.Dispose();

            foreach (var client in Clients)
                client.Dispose();

            m_Clients.Clear();
        }
    }
}
