namespace Unosquare.Ser2Net.Workers;

internal abstract class ConnectionWorkerBase<T> : BackgroundService
    where T : BackgroundService
{
    protected ConnectionWorkerBase(ILogger<T> logger, ConnectionSettingsItem settings)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);

        Logger = logger;
        Settings = settings;
    }

    protected ILogger<T> Logger { get; }

    public ConnectionSettingsItem Settings { get; }
}
