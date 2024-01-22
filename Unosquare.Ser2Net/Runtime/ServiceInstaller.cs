using System.Runtime.Versioning;

namespace Unosquare.Ser2Net.Runtime;

internal static class ServiceInstaller
{
    private const string PlatformNotSupportedMessage = "Your current platform does not support this action.";
    private const string ProcessingErrorMessage = "The process completed, but finished with an error code.";

    public static async Task InstallAsync()
    {
        var resultCode = ServiceInstallerException.DefaultErrorCode;
        try
        {
            if (RuntimeContext.Platform == OSPlatform.Windows)
                resultCode = await InstallWindowsServiceAsync().ConfigureAwait(false);
            else
                throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

            if (resultCode != 0)
                throw new ServiceInstallerException(resultCode, ProcessingErrorMessage);
                
        }
        catch (ServiceInstallerException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ServiceInstallerException(resultCode, ex);
        }
    }

    public static async Task RemoveAsync()
    {
        var resultCode = ServiceInstallerException.DefaultErrorCode;
        try
        {
            if (RuntimeContext.Platform == OSPlatform.Windows)
                resultCode = await RemoveWindowsServiceAsync().ConfigureAwait(false);
            else
                throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

            if (resultCode != 0)
                throw new ServiceInstallerException(resultCode, ProcessingErrorMessage);

        }
        catch (ServiceInstallerException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ServiceInstallerException(resultCode, ex);
        }
    }

    [SupportedOSPlatform(Constants.WindowsOS)]
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

    [SupportedOSPlatform(Constants.WindowsOS)]
    private static async Task<int> InstallWindowsServiceAsync() =>
        await ExecuteElevatedPowerShellScriptAsync(ResourceManager.InstallScriptWindows,
            "i",
            Constants.SerivceKey,
            RuntimeContext.ExecutableFilePath,
            Constants.SerivceName,
            Constants.ServiceDescription).ConfigureAwait(false);

    [SupportedOSPlatform(Constants.WindowsOS)]
    private static async Task<int> RemoveWindowsServiceAsync() =>
        await ExecuteElevatedPowerShellScriptAsync(ResourceManager.InstallScriptWindows,
            "u",
            Constants.SerivceKey).ConfigureAwait(false);
}
