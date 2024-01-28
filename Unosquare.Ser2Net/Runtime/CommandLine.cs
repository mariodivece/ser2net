namespace Unosquare.Ser2Net.Runtime;


/// <summary>
/// Handles command-line arguments operation mode of the <see cref="Program"/>
/// </summary>
internal static class CommandLine
{

    /// <summary>
    /// Handles service/daemon control command line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>
    /// Null if no handling was performed. 0 if successful, anything else if failure.
    /// </returns>
    public static async Task<int?> HandleArgumentsAsync(string[] args)
    {
        const string InstallArgument = "--install";
        const string RemoveArgument = "--remove";

        if (args is null || args.Length <= 0)
            return null;

        ServiceInstallerException? exception = null;
        try
        {
            if (RuntimeContext.RuntimeMode != RuntimeMode.Console)
                throw ServiceInstallerException.InvalidRuntimeMode();

            if (args.Any(c => c.Equals(InstallArgument, StringComparison.OrdinalIgnoreCase)))
                await ServiceInstaller.InstallAsync().ConfigureAwait(false);
            else if (args.Any(c => c.Equals(RemoveArgument, StringComparison.OrdinalIgnoreCase)))
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

        // print out the exception message and set the exit code.
        if (exception is not null)
        {
            Console.WriteLine(exception.ToString(RuntimeContext.IsDevelopment));
            Environment.ExitCode = exception.ErrorCode;
            return exception.ErrorCode;
        }

        // success
        return Constants.ExitCodeSuccess;
    }
}
