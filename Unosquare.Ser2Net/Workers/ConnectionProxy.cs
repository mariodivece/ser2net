namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// A <see cref="IHostedService"/> implementation representing a single serial to
/// TCP connection connection proxy.
/// </summary>
internal sealed class ConnectionProxy
    : ConnectionWorkerBase<ConnectionProxy>, IParentBackgroundService, IConnectionIndex
{
    private readonly List<BackgroundService> _children = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionProxy"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    public ConnectionProxy(
        ILogger<ConnectionProxy> logger,
        IServiceProvider services,
        RootWorkerService parent,
        ConnectionSettingsItem settings)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parent);

        DataBridge = new();
        Services = services;
        Parent = parent;

        // Create the services
        NetServer = Services.CreateInstance<NetServer>(Settings);
        NetDataSender = Services.CreateInstance<NetDataSender>(NetServer, DataBridge);
        NetDataReceiver = Services.CreateInstance<NetDataReceiver>(NetServer, DataBridge);
        SerialPortBroker = Services.CreateInstance<SerialPortBroker>(Settings, DataBridge);

        // register the services as children
        _children.AddRange([
            NetServer,
            NetDataSender,
            NetDataReceiver,
            SerialPortBroker]);
    }

    IReadOnlyList<BackgroundService> IParentBackgroundService.Children => _children;

    public DataBridge DataBridge { get; }

    private NetServer NetServer { get; }

    private IServiceProvider Services { get; }

    private RootWorkerService Parent { get; }

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

    public override void Dispose()
    {
        base.Dispose();
        DataBridge.Dispose();
        SerialPortBroker.Dispose();
        NetDataReceiver.Dispose();
        NetDataSender.Dispose();
        NetServer.Dispose();
    }
}
