namespace Unosquare.Ser2Net.Workers;

internal abstract class WorkerBase<T> : BackgroundService
    where T : BackgroundService
{
    protected WorkerBase(ILogger<T> logger, ServiceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);

        Logger = logger;
        Settings = settings;
    }

    protected ILogger<T> Logger { get; }

    protected ServiceSettings Settings { get; }
}
