using System.Runtime.Versioning;

namespace Unosquare.Ser2Net;

/// <summary>
/// The program holding the man entry point of the application.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry-point for this application.
    /// </summary>
    /// <param name="args">An array of command-line argument strings.</param>
    /// <returns>
    /// Exit-code for the process - 0 for success, else an error code.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        // Handle the special case where command-line arguments were issued.
        var resultCode = await CommandLine.HandleCommandLineArgumentsAsync(args)
            .ConfigureAwait(false);
        
        if (resultCode is not null)
        {
            Environment.ExitCode = resultCode.Value;
            return resultCode.Value;
        }

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

        // TODO: args install/reinstall and uninstall on Linux also (systemd)
        // TODO: Log current settings on startup
        // TODO: Allow for multiple serial ports/servers in config and functionally
    }

}
