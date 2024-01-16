namespace Unosquare.Ser2Net;

/// <summary>
/// The main service host.
/// </summary>
internal sealed class MainHostedService : BackgroundService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainHostedService"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The environment.</param>
    public MainHostedService(
        ILogger<MainHostedService> logger,
        ServiceSettings settings,
        IHostApplicationLifetime lifetime,
        NetworkServer networkServer,
        NetworkDataSender networkDataSender,
        NetworkDataReceiver networkDataReceiver)
        : base()
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(networkServer);
        ArgumentNullException.ThrowIfNull(networkDataSender);
        ArgumentNullException.ThrowIfNull(networkDataReceiver);

        Logger = logger;
        Lifetime = lifetime;
        Settings = settings;
        NetworkServer = networkServer;
        NetworkDataSender = networkDataSender;
        NetworkDataReceiver = networkDataReceiver;
    }

    /// <summary>
    /// Gets the lifetime.
    /// </summary>
    private IHostApplicationLifetime Lifetime { get; }

    private ServiceSettings Settings { get; }

    private ILogger<MainHostedService> Logger { get; }

    private NetworkServer NetworkServer { get; }

    private NetworkDataSender NetworkDataSender { get; }

    private NetworkDataReceiver NetworkDataReceiver { get; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogServiceStarting(nameof(MainHostedService));

        try
        {
            Environment.ExitCode = Constants.ExitCodeSuccess;

            await RunBackgroundServices(stoppingToken,
                NetworkServer,
                NetworkDataReceiver,
                NetworkDataSender);            
        }
        catch (TaskCanceledException)
        {
            // Ignore because shutdown signal was triggered
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

    private static async Task RunBackgroundServices(CancellationToken stoppingToken, params BackgroundService[] workers)
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
