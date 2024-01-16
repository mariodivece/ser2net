using System.Collections.Concurrent;

namespace Unosquare.Ser2Net;

internal sealed class NetworkServer : BackgroundService
{
    private int ClientId;
    private readonly ConcurrentDictionary<int, NetworkClient> m_Clients = new();

    public NetworkServer(ILogger<NetworkServer> logger,
        ServiceSettings settings,
        IServiceProvider services)
        : base()
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(services);

        Logger = logger;
        Settings = settings;
        Services = services;
    }

    private ServiceSettings Settings { get; }

    private IServiceProvider Services { get; }

    private ILogger<NetworkServer> Logger { get; }

    public IReadOnlyList<NetworkClient> Clients => m_Clients.Values.ToArray();

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
                client?.Dispose();
                break;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TcpListener? tcpServer = default;

        try
        {
            tcpServer = new TcpListener(Settings.ServerIP, Settings.ServerPort);
            tcpServer.Start();

            Logger.LogListenerStarted(tcpServer.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await tcpServer.AcceptSocketAsync(stoppingToken).ConfigureAwait(false);
                    var client = ActivatorUtilities.CreateInstance<NetworkClient>(Services, socket);

                    Logger.LogClientAccepted(client.RemoteEndPoint);

                    if (m_Clients.Count >= Constants.MaxClientCount)
                    {
                        Logger.LogConnectionRejectedMax(client.RemoteEndPoint, Constants.MaxClientCount);
                        client.Dispose();
                        client = null;
                        continue;
                    }

                    if (!client.IsConnected)
                    {
                        Logger.LogConnectionNotCompleted(client.RemoteEndPoint);
                        client.Dispose();
                        continue;
                    }

                    m_Clients[Interlocked.Increment(ref ClientId)] = client;
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
                    Logger.LogListenerLoopFailed(tcpServer?.LocalEndpoint, ex.Message, ex);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogListenerFailed(tcpServer?.LocalEndpoint, ex.Message, ex);
            throw;
        }
        finally
        {
            Logger.LogListenerShuttingDown(tcpServer?.LocalEndpoint);
            tcpServer?.Stop();
            tcpServer?.Dispose();

            foreach (var client in Clients)
                client.Dispose();

            m_Clients.Clear();
        }
    }
}
