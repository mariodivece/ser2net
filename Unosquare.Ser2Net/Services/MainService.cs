namespace Unosquare.Ser2Net.Services;

internal class MainService : BackgroundService, IParentBackgroundService
{
    private const string LoggerName = "Host";
    private readonly List<ConnectionProxy> _children = [];

    public MainService(
        ConnectionSettings connections,
        IHostApplicationLifetime lifetime,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Connections = connections;
        Lifetime = lifetime;
        ServiceProvider = serviceProvider;
        LoggerFactory = loggerFactory;

        Logger = loggerFactory.CreateLogger<MainService>();
    }

    public IHostApplicationLifetime Lifetime { get; }

    public IServiceProvider ServiceProvider { get; }

    public ILoggerFactory LoggerFactory { get; }

    public IReadOnlyList<BackgroundService> Children => _children;

    private ILogger<MainService> Logger { get; set; }

    private IReadOnlyList<ConnectionSettingsItem> Connections { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Logger.LogServiceStarting(LoggerName);

        try
        {
            Environment.ExitCode = Constants.ExitCodeSuccess;

            // populate the child background services
            foreach (var connection in  Connections)
                _children.Add(new(this, connection));

            await this.RunChildWorkersAsync(cts.Token)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore because shutdown signal was triggered
            await cts.CancelAsync().ConfigureAwait(false);
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
}
