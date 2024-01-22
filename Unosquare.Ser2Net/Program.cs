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
        if (args.Length > 0)
        {
            var resultCode = 1;
            try
            {
                if (RuntimeContext.RuntimeMode != RuntimeMode.Console)
                    throw new InvalidOperationException("Argument is only valid when issued from a console.");

                if (args.Any(c => c == "--install"))
                {
                    if (RuntimeContext.Platform == OSPlatform.Windows)
                        resultCode = await InstallWindowsServiceAsync().ConfigureAwait(false);
                    else
                        throw new PlatformNotSupportedException("Your current platform does not support this action.");
                }
                else if (args.Any(c => c == "--remove"))
                {
                    if (RuntimeContext.Platform == OSPlatform.Windows)
                        resultCode = await RemoveWindowsServiceAsync().ConfigureAwait(false);
                    else
                        throw new PlatformNotSupportedException("Your current platform does not support this action.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not perform requested action. Exit Code: {resultCode}.\r\n{ex}");
            }

            return resultCode;
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

        // TODO: args install/reinstall and uninstall
        // TODO: Log current settings on startup
        // TODO: Allow for multiple serial ports/servers in config and functionally
    }

    private static async Task<int> ExecuteElevatedPowerShellScriptAsync(string scriptContents, params string[] positionalArgs)
    {
        const string ElevationVerb = "runas";

        var scriptFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(scriptFilePath, scriptContents)
            .ConfigureAwait(false);

        var newScriptFilePath = Path.Combine(Path.GetDirectoryName(scriptFilePath)!, $"{Path.GetFileNameWithoutExtension(scriptFilePath)}.ps1");
        File.Move(scriptFilePath, newScriptFilePath);
        scriptFilePath = newScriptFilePath;

        var arguments = $"-ExecutionPolicy Bypass -File \"{scriptFilePath}\" " +
            string.Join(' ', positionalArgs.Select(c => $"\"{c}\"").ToArray());

        using var process = new Process()
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo()
            {
                Verb = ElevationVerb,
                FileName = "PowerShell.exe",
                CreateNoWindow = true,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = arguments
            }
        };

        var started = process.Start();
        if (!started)
            return 1;

        await process.WaitForExitAsync().ConfigureAwait(false);
        File.Delete(scriptFilePath);
        return process.ExitCode;
    }

    [SupportedOSPlatform("windows")]
    private static async Task<int> InstallWindowsServiceAsync() =>
        await ExecuteElevatedPowerShellScriptAsync(ResourceManager.InstallScriptWindows,
            "i",
            Constants.SerivceKey,
            RuntimeContext.ExecutableFilePath,
            Constants.SerivceName,
            Constants.ServiceDescription).ConfigureAwait(false);

    [SupportedOSPlatform("windows")]
    private static async Task<int> RemoveWindowsServiceAsync() =>
        await ExecuteElevatedPowerShellScriptAsync(ResourceManager.InstallScriptWindows,
            "u",
            Constants.SerivceKey).ConfigureAwait(false);
}
