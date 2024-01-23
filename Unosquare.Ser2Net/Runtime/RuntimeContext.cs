namespace Unosquare.Ser2Net.Runtime;

internal static class RuntimeContext
{
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

    public static string ExecutableFilePath { get; } = Path.GetFullPath(Environment.ProcessPath!);

    public static string ExecutableDirectory { get; } = Path.GetDirectoryName(ExecutableFilePath)!;

    public static string EnvironmentName { get; } = Debugger.IsAttached
        ? Environments.Development
        : Environments.Production;

    public static bool IsProduction => EnvironmentName.Equals(Environments.Production, StringComparison.OrdinalIgnoreCase);

    public static bool IsDevelopment => EnvironmentName.Equals(Environments.Development, StringComparison.OrdinalIgnoreCase);
}
