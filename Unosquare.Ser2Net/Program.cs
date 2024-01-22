using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Principal;

namespace Unosquare.Ser2Net;

/// <summary>
/// The program holding the man entry point of the application.
/// </summary>
internal static class Program
{
    private static readonly Lazy<bool> isElevated = new(() =>
    {
        if (Platform == OSPlatform.Windows)
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            principal.IsInRole(WindowsBuiltInRole.Administrator);
            return true;
        }

        return false;
    });

    /// <summary>
    /// Main entry-point for this application.
    /// </summary>
    /// <param name="args">An array of command-line argument strings.</param>
    /// <returns>
    /// Exit-code for the process - 0 for success, else an error code.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        //if (!IsElevated)
        //{
        //    RestartElevated();
        //    return 0;
        //}

        await InstallWindowsServiceAsync().ConfigureAwait(false);
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

    public static RuntimeMode RuntimeMode { get; } = WindowsServiceHelpers.IsWindowsService()
        ? RuntimeMode.WindowsService
        : SystemdHelpers.IsSystemdService()
        ? RuntimeMode.LinuxSystemd
        : RuntimeMode.Console;

    public static OSPlatform Platform { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? OSPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        ? OSPlatform.Linux
        : OSPlatform.Create("NONE");

    public static bool IsElevated => isElevated.Value;

    public static string ProcessPath { get; } = Path.GetFullPath(Environment.ProcessPath!);

    public static string WorkingDirectory { get; } = Path.GetDirectoryName(ProcessPath)!;

    public static string EnvironmentName { get; } = Debugger.IsAttached
        ? Environments.Development
        : Environments.Production;

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
                //CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                //WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = string.Format(CultureInfo.InvariantCulture,
                    ArgumentsFormat,
                    Constants.SerivceKey,
                    ProcessPath,
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
        process.BeginOutputReadLine();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var exitCode = process.ExitCode;
    }

    private static void RestartElevated()
    {
        const string ElevationVerb = "runas";

        if (string.IsNullOrWhiteSpace(Environment.ProcessPath))
            throw new InvalidOperationException(
                $"Could not read '{nameof(Environment)}.{nameof(Environment.ProcessPath)}'.");

        var processPath = Path.GetFullPath(Environment.ProcessPath);

        using var process = new Process()
        {
            StartInfo = new()
            {
                FileName = processPath,
                Verb = ElevationVerb,
                CreateNoWindow = false,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(processPath)
            }
        };

        if (!process.Start())
            Console.WriteLine("Failed elevation");
    }
}
