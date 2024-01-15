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

    private IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    private IHostApplicationLifetime Lifetime { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    private ILogger<SerialPortServer> Logger { get; }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // TODO: HAndle any dispose logic here
        base.Dispose();
        Logger.LogInformation("Disposing . . .");
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogServiceStarting(nameof(SerialPortServer));

        try
        {
            var settings = ReadSettings();
            var serverAddress = ParseServerAddress(settings);
            var serverEndpoint = new IPEndPoint(serverAddress, settings.ServerPort);
            using var server = new TcpListener(serverEndpoint);
            server.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                using var client = await server.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                Logger.LogInformation("Client {EndPoint} accepted", client.Client.RemoteEndPoint);
                client.NoDelay = true;
                client.SendBufferSize = 1;
                client.ReceiveBufferSize = 1;
                var stream = client.GetStream();
                var bufferSize = 1; // Math.Max(1024, settings.BaudRate / 8);
                using var readBuffer = MemoryPool<byte>.Shared.Rent(bufferSize);
                var receivedText = new StringBuilder(bufferSize);

                await stream.WriteAsync(Encoding.UTF8.GetBytes("Hello from TCP!"), stoppingToken);

                while (!client.Client.Poll(1, SelectMode.SelectRead) && !stoppingToken.IsCancellationRequested)
                {
                    receivedText.Clear();

                    do
                    {
                        var readByteCount = await stream.ReadAsync(readBuffer.Memory, stoppingToken);
                        if (readByteCount > 0)
                        {
                            var readText = Encoding.UTF8.GetString(readBuffer.Memory.Span[..readByteCount]);
                            receivedText.Append(readText);
                        }
                    } while (client.Available > 0);
                    
                    if (receivedText.Length > 0)
                    {
                        var sendBuffer = Encoding.UTF8.GetBytes(receivedText.ToString());
                        await stream.WriteAsync(sendBuffer, stoppingToken);
                    }
                    
                }

                Logger.LogInformation("Client {EndPoint} disconnected.", client.Client.RemoteEndPoint);
            }

            server.Stop();

            Environment.ExitCode = Constants.ExitCodeSuccess;
        }
        catch(Exception ex)
        {
            Environment.ExitCode = Constants.ExitCodeFailure;
        }
        finally
        {
            Lifetime.StopApplication();
        }
    }

    private ServiceSettings ReadSettings()
    {
        var settings = new ServiceSettings();
        Configuration.GetRequiredSection(ServiceSettings.SectionName).Bind(settings);
        return settings;
    }

    private IPAddress ParseServerAddress(ServiceSettings settings)
    {
        if (!IPAddress.TryParse(settings.ServerIP, out var serverIP))
        {
            Logger.LogWarning("Settings Server IP '{ServerIP}' is invalid. Will use all available local addresses.", settings.ServerIP);
            serverIP = Constants.DefaultServerIP;
        }
        else
        {
            Logger.LogInformation("Server IP: {ServerIP}", settings.ServerIP);
        }

        return serverIP;
    }
}
