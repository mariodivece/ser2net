using System.Net;

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

            if (!IPAddress.TryParse(settings.ServerIP, out var serverIP))
            {
                Logger.LogWarning("Settings Server IP '{ServerIP}' is invalid. Will use all available local addresses.", settings.ServerIP);
                serverIP = IPAddress.Any;
            }
            else
            {
                Logger.LogInformation("Server IP: {ServerIP}", settings.ServerIP);
            }
                

            var count = 0;
            while (!stoppingToken.IsCancellationRequested && count < 3)
            {
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
                Logger.LogInformation(settings.Message, count);
                count++;
            }

            Environment.ExitCode = Constants.ExitCodeSuccess;
        }
        catch
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
}
