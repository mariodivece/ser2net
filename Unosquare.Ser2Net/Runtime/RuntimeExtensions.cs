namespace Unosquare.Ser2Net.Runtime;

internal static class RuntimeExtensions
{
    /// <summary>
    /// Shorthand method for creating DI-injected instances.
    /// </summary>
    /// <param name="services">The services provider.</param>
    /// <param name="parameters">A variable-length parameters list containing constructor parameters.</param>
    /// <returns>
    /// A new instance of the requested service.
    /// </returns>
    public static T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceProvider services, params object[] parameters) =>
        ActivatorUtilities.CreateInstance<T>(services, parameters);


    /// <summary>
    /// Executes the child background services of the provided parent background service.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="parent">The parent containing the child background services.</param>
    /// <param name="stoppingToken">A token that allows processing to be cancelled.</param>
    /// <returns>
    /// An awaitable task.
    /// </returns>
    public static async Task RunChildWorkersAsync(
        this IParentBackgroundService parent,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.Children is null || parent.Children.Count == 0)
            return;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var children = parent.Children;

        var tasks = new List<Task>(children.Count);
        foreach (var worker in children)
        {
            if (worker is null)
                continue;

            _ = worker.StartAsync(cts.Token);
            if (worker.ExecuteTask is not null)
                tasks.Add(worker.ExecuteTask!);
        }

        // We use WehnAny (as opposed to WhenAll)
        // because if a single subsystem fails, the rest
        // of them simply won't work.
        await Task.WhenAny(tasks).ConfigureAwait(false);

        cts.CancelAfter(1000);
        var stopTasks = children.Select(c => c.StopAsync(cts.Token)).ToArray();
        await Task.WhenAll(stopTasks).ConfigureAwait(false);
    }
}
