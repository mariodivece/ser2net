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
        //await InstallWindowsServiceAsync().ConfigureAwait(false);
        //await InstallWindowsServiceAsync().ConfigureAwait(false);
        //return 0;
        SampleWindowsIntallScript();
        return 0;
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

    [SupportedOSPlatform("windows")]
    private static async Task InstallWindowsServiceAsync()
    {
        const string ElevationVerb = "runas";
        const string ArgumentsFormat =
            "create {0} binpath=\"{1}\" displayname=\"{2}\" start=auto";
        
        using var process = new Process()
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo()
            {
                Verb = ElevationVerb,
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe"),
                CreateNoWindow = true,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                //RedirectStandardError = true,
                //WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = string.Format(CultureInfo.InvariantCulture,
                    ArgumentsFormat,
                    Constants.SerivceKey,
                    RuntimeContext.ExecutableFilePath,
                    Constants.SerivceName)
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            Console.WriteLine(e.Data);
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            Console.WriteLine(e.Data);
        };

        var started = process.Start();
        //process.BeginOutputReadLine();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var exitCode = process.ExitCode;
    }

    private static void SampleWindowsIntallScript()
    {
        
        var script = ResourceManager.InstallScriptWindows;
    }

}
