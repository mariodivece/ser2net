namespace Unosquare.Ser2Net.Workers;


/// <summary>
/// Represents the main application service that will bootstrap
/// the required <see cref="ConnectionProxy"/> services according to
/// the <see cref="ConnectionSettingsItem"/> elements contained in the
/// <see cref="ConnectionSettings"/> singleton.
/// </summary>
internal sealed class RootWorkerService :
    BackgroundService,
    IParentBackgroundService
{
    private const string LoggerName = "Orchestrator";
    private readonly List<ConnectionProxy> _children = [];

    public RootWorkerService(
        ILogger<RootWorkerService> logger,
        ConnectionSettings connections,
        IHostApplicationLifetime lifetime,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        Connections = connections;
        Lifetime = lifetime;
        Services = services;
        Logger = logger;
    }

    IReadOnlyList<BackgroundService> IParentBackgroundService.Children => _children;

    private IHostApplicationLifetime Lifetime { get; }

    private IServiceProvider Services { get; }

    private ILogger<RootWorkerService> Logger { get; set; }

    private IReadOnlyList<ConnectionSettingsItem> Connections { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Logger.LogServiceStarting(LoggerName);

        try
        {
            Environment.ExitCode = Constants.ExitCodeSuccess;

            // populate the child background services
            foreach (var connection in Connections)
            {
                var connectionProxy = Services.CreateInstance<ConnectionProxy>(this, connection);
                _children.Add(connectionProxy);
            }

            await this.RunChildWorkersAsync(cts.Token)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore because shutdown signal was triggered
        }
        catch (Exception ex)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            Logger.LogServiceError(LoggerName, ex);
            Environment.ExitCode = Constants.ExitCodeFailure;
        }
        finally
        {
            Lifetime.StopApplication();
        }
    }
}
