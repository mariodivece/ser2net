namespace Unosquare.Ser2Net.Services;

/// <summary>
/// The main hosted service that boostraps all of the sub-services.
/// </summary>
internal sealed class MainHostedService : WorkerBase<MainHostedService>
{
    private const string LoggerName = "Service";

    /// <summary>
    /// Initializes a new instance of the <see cref="MainHostedService"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    public MainHostedService(
        ILogger<MainHostedService> logger,
        ServiceSettings settings,
        IHostApplicationLifetime lifetime,
        NetServer networkServer,
        NetDataSender networkDataSender,
        NetDataReceiver networkDataReceiver,
        SerialPortBroker portBroker)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(networkServer);
        ArgumentNullException.ThrowIfNull(networkDataSender);
        ArgumentNullException.ThrowIfNull(networkDataReceiver);
        ArgumentNullException.ThrowIfNull(portBroker);

        Lifetime = lifetime;
        NetworkServer = networkServer;
        NetworkDataSender = networkDataSender;
        NetworkDataReceiver = networkDataReceiver;
        PortBroker = portBroker;
    }

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    private IHostApplicationLifetime Lifetime { get; }

    private NetServer NetworkServer { get; }

    private NetDataSender NetworkDataSender { get; }

    private NetDataReceiver NetworkDataReceiver { get; }

    private SerialPortBroker PortBroker { get; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogServiceStarting(LoggerName);

        try
        {
            Environment.ExitCode = Constants.ExitCodeSuccess;

            await RunBackgroundServicesAsync(stoppingToken,
                NetworkServer,
                NetworkDataReceiver,
                NetworkDataSender,
                PortBroker);
        }
        catch (TaskCanceledException)
        {
            // Ignore because shutdown signal was triggered
        }
        catch (Exception ex)
        {
            Logger.LogServiceError(LoggerName, ex);
            Environment.ExitCode = Constants.ExitCodeFailure;
        }
        finally
        {
            Lifetime.StopApplication();
        }
    }

    private static async Task RunBackgroundServicesAsync(CancellationToken stoppingToken, params BackgroundService[] workers)
    {
        var tasks = new List<Task>(workers.Length);
        foreach (var worker in workers)
        {
            if (worker is null)
                continue;

            _ = worker.StartAsync(stoppingToken);
            tasks.Add(worker.ExecuteTask!);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
