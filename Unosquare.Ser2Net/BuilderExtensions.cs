using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using System.Runtime.Versioning;

namespace Unosquare.Ser2Net;

/// <summary>
/// Host builder extension methods.
/// </summary>
internal static class BuilderExtensions
{
    /// <summary>
    /// Extension method that configures the lifetime and logging of the host,
    /// depending on its platform.
    /// </summary>
    /// <typeparam name="T">The host builder type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>
    /// The same object for fluent API support.
    /// </returns>
    public static T ConfigureLifetimeAndLogging<T>(this T builder)
        where T : IHostBuilder
    {
        if (WindowsServiceHelpers.IsWindowsService())
        {
            builder.UseWindowsService();
            builder.ConfigureLogging((context, logging) =>
            {
                // we don't want to use the event log
                // we will just use the file.
                logging.ClearProviders();
            });
        }
        else if (SystemdHelpers.IsSystemdService())
        {
            builder.UseSystemd();
            builder.ConfigureLogging((context, logging) =>
            {
                logging.RemoveLoggingProvidersExcept(
                    typeof(ConsoleLoggerProvider));
            });
        }
        else
        {
            builder.UseConsoleLifetime();
        }

        if (WindowsServiceHelpers.IsWindowsService())
        {
            builder
                .UseWindowsService(options => options.ServiceName = Constants.SerivceName)
                .ConfigureWindowsEventLogLogging();
        }
        else if (SystemdHelpers.IsSystemdService())
        {
            builder.UseSystemd();
            builder.ConfigureLogging((context, logging) =>
            {
                logging.RemoveLoggingProvidersExcept(
                    typeof(ConsoleLoggerProvider));
            });
        }
        else
        {
            // Running as a console application
            builder
                .UseConsoleLifetime()
                .ConfigureLogging((context, logging) =>
                {
                    var serilogLogger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .MinimumLevel.Is(context.HostingEnvironment.IsProduction() ? Serilog.Events.LogEventLevel.Information : Serilog.Events.LogEventLevel.Debug)
                        .CreateLogger();

                    /*
                     *        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(Debugger.IsAttached ? LogEventLevel.Verbose : LogEventLevel.Information)
            .WriteTo.Async(f => f.File(@"f:\log\log.txt", rollingInterval: RollingInterval.Day))
    .CreateLogger(); 
                     * 
                    */

                    // Set a global static logger
                    Log.Logger = serilogLogger;

                    logging
                        .ClearProviders()
                        .AddSerilog(serilogLogger, dispose: true)
                        .SetMinimumLevel(LogLevel.Trace);
                });
        }

        // set the minimum log level according to environment
        builder.ConfigureLogging((context, logging) =>
        {
            var logLevel = context.HostingEnvironment.IsProduction()
                ? LogLevel.Information
                : LogLevel.Trace;

            logging.SetMinimumLevel(logLevel);
        });

        return builder;
    }

    /// <summary>
    /// Adds the serial port server hosted service to the host builder.
    /// </summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <returns>
    /// The host builder for fluent API support.
    /// </returns>
    public static T UseMainHostedService<T>(this T builder)
        where T : IHostBuilder
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddHostedService<MainHostedService>()
                .AddSingleton<ServiceSettings>()
                .AddSingleton<DataBridge>()
                .AddSingleton<NetServer>()
                .AddSingleton<NetDataReceiver>()
                .AddSingleton<NetDataSender>()
                .AddSingleton<SerialPortBroker>()
                .AddTransient<NetworkClient>();
        });

        return builder;
    }

    [SupportedOSPlatform("windows")]
    private static T ConfigureWindowsEventLogLogging<T>(this T builder)
        where T : IHostBuilder
    {
        // TODO: Need to register EventSource name in EventLog if it does not exist.
        // This requires elevation. Unsure is windows service automatically adds. Will
        // nee to test.

        builder.ConfigureLogging((context, logging) =>
        {
            // TODO: This is not working too well.
            logging
                .ClearProviders()
                .AddEventLog(config =>
                {
                    config.SourceName = ".NET Runtime"; //Constants.SerivceName,
                    config.Filter = (s, e) => true;
                    config.LogName = Constants.WindowsLogName;
                })
                .SetMinimumLevel(LogLevel.Trace);
        });

        return builder;
    }

    private static ILoggingBuilder RemoveLoggingProvidersExcept(this ILoggingBuilder logging, params Type[] keepTypes)
    {
        var providers = logging.Services.Where(c => c.ServiceType == typeof(ILoggerProvider)).ToArray();
        foreach (var provider in providers)
        {
            if (!keepTypes.Contains(provider.ImplementationType))
                logging.Services.Remove(provider);
        }

        return logging;
    }
}
