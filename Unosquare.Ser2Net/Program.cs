using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Unosquare.Ser2Net;

/// <summary>
/// The program holding the man entry point of the application.
/// </summary>
internal static class Program
{
    private static readonly Lazy<RuntimeMode> runtimeMode = new(() => 
        WindowsServiceHelpers.IsWindowsService()
        ? RuntimeMode.WindowsService
        : SystemdHelpers.IsSystemdService()
        ? RuntimeMode.LinuxSystemd
        : RuntimeMode.Console);

    private static readonly Lazy<OSPlatform> platform = new(() => 
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? OSPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        ? OSPlatform.Linux
        : OSPlatform.Create("NONE"));

    /// <summary>
    /// Main entry-point for this application.
    /// </summary>
    /// <param name="args">An array of command-line argument strings.</param>
    /// <returns>
    /// Exit-code for the process - 0 for success, else an error code.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        // Here we create the host builder from scratch
        // as opposed to using Host.CreateDefaultBuilder(args)
        // because we need to keep this as lightweight and speceific
        // as possible without adding a bunch of unnecessary service
        // dependencies.
        var builder = new HostBuilder()
            .ConfigureHostEnvironment()
            .ConfigureLifetimeAndLogging()
            .UseMainHostedService();

        using var host = builder.Build();
        await host.RunAsync(cts.Token).ConfigureAwait(false);
        return Environment.ExitCode;

        // TODO: args install/reinstall and uninstall
        // TODO: Log current settings on startup
        // TODO: Allow for multiple serial ports/servers in config and functionally
    }

    public static RuntimeMode RuntimeMode => runtimeMode.Value;

    public static OSPlatform Platform => platform.Value;
}
