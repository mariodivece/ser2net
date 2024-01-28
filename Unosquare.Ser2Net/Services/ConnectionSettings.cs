using System.Collections;

namespace Unosquare.Ser2Net.Services;

/// <summary>
/// Holds a list of settings to be used for each <see cref="ConnectionProxy"/>.
/// </summary>
internal sealed class ConnectionSettings : IReadOnlyList<ConnectionSettingsItem>
{
    private const string ConnectionsSectionName = "Connections";
    private readonly List<ConnectionSettingsItem> _connections = [];
    

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettings"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public ConnectionSettings(IConfiguration configuration, ILogger<ConnectionSettings> logger)
    {
        Logger = logger;
        var configItems = configuration.GetSection(ConnectionsSectionName).GetChildren().ToArray();
        var connectionIndex = 0;
        foreach (var item in configItems)
            _connections.Add(new(connectionIndex++, item, logger));
    }

    private ILogger<ConnectionSettings> Logger { get; }

    /// <inheritdoc/>
    public ConnectionSettingsItem this[int index] =>
        ((IReadOnlyList<ConnectionSettingsItem>)_connections)[index];

    /// <inheritdoc/>
    public int Count => ((IReadOnlyCollection<ConnectionSettingsItem>)_connections).Count;

    /// <inheritdoc/>
    public IEnumerator<ConnectionSettingsItem> GetEnumerator() =>
        ((IEnumerable<ConnectionSettingsItem>)_connections).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() =>
        ((IEnumerable)_connections).GetEnumerator();
}
