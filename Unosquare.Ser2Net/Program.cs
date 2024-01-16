namespace Unosquare.Ser2Net;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        var builder = Host
            .CreateDefaultBuilder(args)
            .ConfigureLifetimeAndLogging()
            .UseSerialPortServer()
            .ConfigureServices((context, services) =>
            {
                services
                    .AddSingleton<ServiceSettings>()
                    .AddTransient<NetworkClient>();
            });
        
        using var host = builder.Build();
        
        await host.RunAsync(cts.Token).ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
