using Microsoft.Extensions.Logging.Console;
using Serilog;
using System.Runtime.Versioning;

namespace Unosquare.Ser2Net.Runtime;

/// <summary>
/// Host builder extension methods.
/// </summary>
internal static class BuilderExtensions
{
    /// <summary>
    /// Begins configuration of the builder with basic environment information.
    /// </summary>
    /// <typeparam name="T">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>
    /// The same object for fluent API support.
    /// </returns>
    public static T ConfigureHostEnvironment<T>(this T builder)
        where T : IHostBuilder
    {
        builder
            .ConfigureAppConfiguration((context, config) =>
            {
                // setup basic host environment information
                var env = context.HostingEnvironment;
                env.EnvironmentName = RuntimeContext.EnvironmentName;
                env.ContentRootPath = RuntimeContext.ExecutableDirectory;
                env.ApplicationName = Constants.SerivceName;

                // add the json configuration file
                config.AddJsonFile(Constants.SettingsFilename,
                    optional: true,
                    reloadOnChange: false);
            })
            .ConfigureHostOptions((context, options) =>
            {
                options.ServicesStartConcurrently = true;
                options.ServicesStopConcurrently = false;
                options.BackgroundServiceExceptionBehavior =
                    BackgroundServiceExceptionBehavior.StopHost;
            })
            .ConfigureLogging((context, logging) =>
            {
                // set the minimum log level according to environment
                var logLevel = context.HostingEnvironment.IsProduction()
                    ? LogLevel.Information
                    : LogLevel.Trace;

                logging.SetMinimumLevel(logLevel);
            });

        return builder;
    }

    /// <summary>
    /// Extension method that configures the lifetime and logging
    /// providers for the host. This is all dependent of the host
    /// platofrm and runtime information.
    /// </summary>
    /// <typeparam name="T">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>
    /// The same object for fluent API support.
    /// </returns>
    public static T ConfigureLifetimeAndLogging<T>(this T builder)
        where T : IHostBuilder
    {
        switch (RuntimeContext.RuntimeMode)
        {
            case RuntimeMode.WindowsService:
                if (RuntimeContext.Platform != OSPlatform.Windows)
                    throw new PlatformNotSupportedException();

                builder
                    .UseWindowsService(options => options.ServiceName = Constants.SerivceName)
                    .ClearLoggingProviders()
                    .AddWindowsEventLogging()
                    .AddSerilogLogging(useConsole: false, useFiles: true);

                break;
            case RuntimeMode.LinuxSystemd:

                builder
                    .UseSystemd()
                    .ClearLoggingProvidersExcept(typeof(ConsoleLoggerProvider))
                    .AddSerilogLogging(useConsole: false, useFiles: true); ;

                break;
            case RuntimeMode.Console:

                builder
                    .UseConsoleLifetime()
                    .ClearLoggingProviders()
                    .AddSerilogLogging(useConsole: true, useFiles: true);

                break;
            default:
                throw new PlatformNotSupportedException();
        }

        return builder;
    }

    /// <summary>
    /// Configures and adds the <see cref="ConnectionProxy"/> service to the host builder
    /// and all of its dependencies.
    /// </summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>
    /// The same object for fluent API support.
    /// </returns>
    public static T UseMainHostedService<T>(this T builder)
        where T : IHostBuilder
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddHostedService<MainService>()
                .AddSingleton<ConnectionSettings>();

            //    .AddSingleton<DataBridge>()
            //    .AddSingleton<NetServer>()
            //    .AddSingleton<NetDataReceiver>()
            //    .AddSingleton<NetDataSender>()
            //    .AddSingleton<SerialPortBroker>();
        });

        return builder;
    }

    public static async Task RunChildWorkersAsync(
        this IParentBackgroundService parent,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.Children is null || parent.Children.Count == 0)
            return;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var children = parent.Children;

        var tasks = new List<Task>(children.Count);
        foreach (var worker in children)
        {
            if (worker is null)
                continue;

            _ = worker.StartAsync(cts.Token);
            tasks.Add(worker.ExecuteTask!);
        }

        // We use WehnAny (as opposed to WhenAll)
        // because if a single subsystem fails, the rest
        // of them simply won't work.
        await Task.WhenAny(tasks).ConfigureAwait(false);

        cts.CancelAfter(1000);
        var stopTasks = children.Select(c => c.StopAsync(cts.Token)).ToArray();
        await Task.WhenAll(stopTasks).ConfigureAwait(false);
    }

    private static T AddSerilogLogging<T>(this T builder, bool useConsole, bool useFiles)
        where T : IHostBuilder
    {
        if (!useConsole && !useFiles)
            return builder;

        builder.ConfigureLogging((context, logging) =>
        {
            var serilogLevel = context.HostingEnvironment.IsProduction()
                ? LogEventLevel.Information
                : LogEventLevel.Verbose;

            // start with basic console configuration
            var serilogLoggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(serilogLevel);

            // add console logging if applicable
            if (useConsole)
            {
                serilogLoggerConfig.WriteTo
                    .Console(formatProvider: CultureInfo.InvariantCulture);
            }

            // now attempt to configure file logs
            if (useFiles)
            {
                try
                {
                    var logsPath = Path.Combine(
                        Path.GetFullPath(context.HostingEnvironment.ContentRootPath),
                        Constants.LogsDirectoryName);

                    if (!Directory.Exists(logsPath))
                        Directory.CreateDirectory(logsPath);

                    var logFileTemplate = Path.Combine(logsPath, Constants.LogsBaseFileName);

                    serilogLoggerConfig
                        .WriteTo.Async(f => f.File(
                            logFileTemplate,
                            rollingInterval: RollingInterval.Day,
                            formatProvider: CultureInfo.InvariantCulture,
                            shared: true,
                            flushToDiskInterval: Constants.LogsFileFlushInterval,
                            retainedFileCountLimit: Constants.LogsFileCountMax));
                }
                catch
                {
                    // ignore
                }
            }

            // create the serilog logger
            var serilogLogger = serilogLoggerConfig.CreateLogger();

            // Set a global static logger (just in case we need it)
            Log.Logger = serilogLogger;
            logging.AddSerilog(serilogLogger, dispose: true);
        });

        return builder;
    }

    [SupportedOSPlatform(Constants.WindowsOS)]
    private static T AddWindowsEventLogging<T>(this T builder)
        where T : IHostBuilder
    {
        // TODO: Need to register EventSource name in EventLog if it does not exist.
        // This requires elevation. Unsure is windows service automatically adds. Will
        // need to test and understand more.

        builder.ConfigureLogging((context, logging) =>
        {
            // TODO: This is not working too well.
            logging
                .AddEventLog(config =>
                {
                    config.LogName = Constants.WindowsLogName;
                    config.SourceName = ".NET Runtime"; //Constants.SerivceName,
                    config.Filter = (s, e) => true;
                })
                .SetMinimumLevel(LogLevel.Trace);
        });

        return builder;
    }

    private static T ClearLoggingProviders<T>(this T builder)
        where T : IHostBuilder
    {
        builder.ConfigureLogging((contexnt, logging) =>
        {
            logging.ClearProviders();
        });

        return builder;
    }

    private static T ClearLoggingProvidersExcept<T>(this T builder, params Type[] keepTypes)
        where T : IHostBuilder
    {
        keepTypes ??= [];

        builder.ConfigureLogging((context, logging) =>
        {
            var providers = logging.Services.Where(c => c.ServiceType == typeof(ILoggerProvider)).ToArray();
            foreach (var provider in providers)
            {
                if (!keepTypes.Contains(provider.ImplementationType))
                    logging.Services.Remove(provider);
            }
        });

        return builder;
    }
}
