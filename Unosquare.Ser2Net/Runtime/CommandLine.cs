namespace Unosquare.Ser2Net.Runtime;

internal static class CommandLine
{
    private const string InvalidArgumentMessage = "Invalid argument.";
    private const string ActionNotPerformedMessage = "Could not perform requested action. Exit Code:";
    private const string BadRuntimeModeMessage = "Argument is only valid when issued from the console.";

    public static async Task<int?> HandleCommandLineArgumentsAsync(string[] args)
    {
        if (args is null || args.Length <= 0)
            return null;

        var resultCode = 0;
        try
        {
            if (RuntimeContext.RuntimeMode != RuntimeMode.Console)
                throw new InvalidOperationException(BadRuntimeModeMessage);

            if (args.Any(c => c == "--install"))
                await ServiceInstaller.InstallAsync().ConfigureAwait(false);
            else if (args.Any(c => c == "--remove"))
                await ServiceInstaller.RemoveAsync().ConfigureAwait(false);
            else
                throw new ServiceInstallerException(-64, InvalidArgumentMessage);
        }
        catch (ServiceInstallerException ex)
        {
            resultCode = ex.ResultCode;
            Console.WriteLine($"{ActionNotPerformedMessage} {resultCode}.\r\n{ex}");
        }
        catch (Exception ex)
        {
            resultCode = ServiceInstallerException.DefaultErrorCode;
            Console.WriteLine($"{ActionNotPerformedMessage} {resultCode}.\r\n{ex}");
        }

        return resultCode;
    }
}
