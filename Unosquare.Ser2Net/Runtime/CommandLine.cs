namespace Unosquare.Ser2Net.Runtime;

internal static class CommandLine
{
    public static async Task<int?> HandleCommandLineArgumentsAsync(string[] args)
    {
        if (args is null || args.Length <= 0)
            return null;

        ServiceInstallerException? exception = null;
        try
        {
            if (RuntimeContext.RuntimeMode != RuntimeMode.Console)
                throw ServiceInstallerException.InvalidRuntimeMode();

            if (args.Any(c => c == "--install"))
                await ServiceInstaller.InstallAsync().ConfigureAwait(false);
            else if (args.Any(c => c == "--remove"))
                await ServiceInstaller.RemoveAsync().ConfigureAwait(false);
            else
                throw ServiceInstallerException.InvalidArgument();;
        }
        catch (ServiceInstallerException ex)
        {
            exception = ex;
        }
        catch (Exception ex)
        {
            var sex = ServiceInstallerException.GeneralFailure(ex);
            exception = sex;
        }

        if (exception is not null)
        {
            Console.WriteLine(exception.ToString(RuntimeContext.IsDevelopment));
            return exception.ErrorCode;
        }

        return 0;
    }
}
