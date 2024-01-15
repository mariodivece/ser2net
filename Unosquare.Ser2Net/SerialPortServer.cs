using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

    public ServiceSettings Settings { get; private set; }

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
        var builder = new StringBuilder();

        while (!cancellation.IsCancellationRequested)
        {
            if (Clients.Count <= 0)
            {
                await Task.Delay(1).ConfigureAwait(false);
                continue;
            }

            var disconnectedClients = new List<SerialPortClient>(Clients.Count);
            var readSomething = false;

            foreach (var client in Clients)
            {
                try
                {
                    var readBuffer = await client.ReadAsync(cancellation).ConfigureAwait(false);
                    if (readBuffer.Length > 0)
                        builder.Append(Encoding.UTF8.GetString(readBuffer.Span));
                }
                catch
                {
                    disconnectedClients.Add(client);
                }
            }

            if (builder.Length > 0)
            {
                readSomething = true;
                foreach (var client in Clients.Except(disconnectedClients))
                {
                    try
                    {
                        await client.WriteAsync(Encoding.UTF8.GetBytes(builder.ToString()), cancellation).ConfigureAwait(false);
                    }
                    catch
                    {
                        disconnectedClients.Add(client);
                    }
                }

                builder.Clear();
            }

            foreach (var disconnectedClient in disconnectedClients)
            {
                Clients.Remove(disconnectedClient);
                disconnectedClient.Dispose();
            }

            if (!readSomething)
                await Task.Delay(1).ConfigureAwait(false);
        }

        DisconnectClients();
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
        var settings = new ServiceSettings();
        Configuration.GetRequiredSection(ServiceSettings.SectionName).Bind(settings);
        Settings = settings;
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
