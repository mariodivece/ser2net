using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Unosquare.Ser2Net;

/// <summary>
/// The main service host.
/// </summary>
internal sealed class ServiceHost : BackgroundService
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHost"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The environment.</param>
    public ServiceHost(
        ILogger<ServiceHost> logger,
        ServiceSettings settings,
        IHostApplicationLifetime lifetime,
        IServiceProvider services)
        : base()
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(services);

        Logger = logger;
        Lifetime = lifetime;
        Settings = settings;
        Services = services;
    }

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    private IHostApplicationLifetime Lifetime { get; }

    private ServiceSettings Settings { get; }

    private IServiceProvider Services { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    private ILogger<ServiceHost> Logger { get; }

    private TcpListener? TcpServer { get; set; }

    private List<NetworkClient> Clients { get; } = [];

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
            TcpServer = new TcpListener(Settings.ServerIP, Settings.ServerPort);
            TcpServer.Start();

            Logger.LogListenerStarted(TcpServer.LocalEndpoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await TcpServer.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
                    var client = ActivatorUtilities.CreateInstance<NetworkClient>(Services, socket);

                    Logger.LogClientAccepted(client.RemoteEndPoint);

                    if (Clients.Count >= Constants.MaxClientCount)
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
            Logger.LogListenerShuttingDown(TcpServer?.LocalEndpoint);
            TcpServer?.Stop();
            TcpServer?.Dispose();
            TcpServer = null;

        }
    }

    public async Task RunEchoServer(CancellationToken cancellation)
    {
        var byteQueue = new BufferQueue<byte>();
        var clientsSyncRoot = new SemaphoreSlim(1, 1);
        var disconnectedClients = new ConcurrentQueue<NetworkClient>();

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
        async Task<NetworkClient[]> getCurrentClients()
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
        Logger.LogServiceStarting(nameof(ServiceHost));

        try
        {
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
}
