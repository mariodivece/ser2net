using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging.EventSource;
using Serilog;
using System.Runtime.InteropServices;

namespace Unosquare.Ser2Net;

internal static class HostBuilderExtensions
{
    /// <summary>
    /// Extension method that configures the lifetime and logging of the host.
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
            builder
                .UseWindowsService(options => options.ServiceName = Constants.SerivceName)
                .ConfigureEventLogLogging();
        }
        else if (SystemdHelpers.IsSystemdService())
        {
            builder.UseSystemd();
            builder.ConfigureLogging((context, logging) =>
            {
                logging.RemoveLoggingProvidersExcept(typeof(ConsoleLoggerProvider));
            });
        }
        else
        {
            // Running as a console application
            builder.UseConsoleLifetime();
            builder.ConfigureLogging((context, logging) =>
            {
                var serilogLogger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .MinimumLevel.Verbose()
                    .CreateLogger();

                // Set a global static logger
                Log.Logger = serilogLogger;

                logging
                    .ClearProviders()
                    .AddSerilog(serilogLogger, true);
            });
        }

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
    public static T UseSerialPortServer<T>(this T builder)
        where T : IHostBuilder
    {
        builder.ConfigureServices(services =>
        {
            services.AddHostedService<ServiceHost>();
        });

        return builder;
    }

    private static T ConfigureEventLogLogging<T>(this T builder)
        where T : IHostBuilder
    {
        builder.ConfigureLogging((context, logging) =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            logging.RemoveLoggingProvidersExcept(
                typeof(EventLogLoggerProvider),
                typeof(EventSourceLoggerProvider));

            // TODO: Need to register EventSource name in EventLog if it does not exist.
            // This requires elevation. Unsure is windows service automatically adds. Will
            // nee to test.

            //LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(logging.Services);
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
