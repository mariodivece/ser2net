namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// A <see cref="IHostedService"/> implementation representing a single serial to
/// TCP connection orchestrator.
/// </summary>
internal sealed class ConnectionProxy
    : ConnectionWorkerBase<ConnectionProxy>, IParentBackgroundService, IConnectionIndex
{
    private readonly List<BackgroundService> _children = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionProxy"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    public ConnectionProxy(MainService mainService, ConnectionSettingsItem settings)
        : base(mainService.LoggerFactory.CreateLogger<ConnectionProxy>(), settings)
    {
        ArgumentNullException.ThrowIfNull(mainService);
        Parent = mainService;
        ConnectionIndex = settings.ConnectionIndex;

        // Create the services
        var log = Parent.LoggerFactory;
        NetServer = new(log.CreateLogger<NetServer>(), Settings, Parent.ServiceProvider);
        NetDataSender = new(log.CreateLogger<NetDataSender>(), Settings, DataBridge, NetServer);
        NetDataReceiver = new(log.CreateLogger<NetDataReceiver>(), Settings, DataBridge, NetServer);
        SerialPortBroker = new(log.CreateLogger<SerialPortBroker>(), Settings, DataBridge);

        // register the services as children
        _children.AddRange([
            NetServer,
            NetDataSender,
            NetDataReceiver,
            SerialPortBroker]);
    }

    public int ConnectionIndex { get; }

    public IReadOnlyList<BackgroundService> Children => _children;

    private MainService Parent { get; }

    private DataBridge DataBridge { get; } = new();

    private NetServer NetServer { get; }

    private NetDataSender NetDataSender { get; }

    private NetDataReceiver NetDataReceiver { get; }

    private SerialPortBroker SerialPortBroker { get; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Logger.LogConnectionStarting(ConnectionIndex);

        try
        {
            await this.RunChildWorkersAsync(cts.Token)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore because shutdown signal was triggered
        }
        catch (Exception ex)
        {
            Logger.LogConnectionError(ConnectionIndex, ex);
        }
        finally
        {
            // signal cancellation for remaining tasks
            await cts.CancelAsync().ConfigureAwait(false);
        }
    }
}
