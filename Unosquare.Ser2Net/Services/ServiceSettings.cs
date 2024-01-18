namespace Unosquare.Ser2Net.Services;

internal class ServiceSettings
{
    private const string LoggerName = "Settings";

    public ServiceSettings(IConfiguration configuration, ILogger<ServiceSettings> logger)
    {
        Logger = logger;

        if (TryRead<string>(configuration, nameof(Message), out var message))
            Message = message;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(Message), Message);

        if (TryRead<IPAddress>(configuration, nameof(ServerIP), out var serverIP))
            ServerIP = serverIP;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(ServerIP), ServerIP.ToString());

        if (TryRead<int>(configuration, nameof(ServerPort), out var serverPort))
            ServerPort = serverPort;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(ServerPort), ServerPort.ToString(CultureInfo.InvariantCulture));

        if (TryRead<string>(configuration, nameof(PortName), out var portName))
            PortName = portName;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(PortName), PortName);

        if (TryRead<int>(configuration, nameof(BaudRate), out var baudRate))
            BaudRate = baudRate;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(BaudRate), BaudRate.ToString(CultureInfo.InvariantCulture));

        if (TryRead<int>(configuration, nameof(DataBits), out var dataBits))
            DataBits = dataBits;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(DataBits), DataBits.ToString(CultureInfo.InvariantCulture));

        if (TryRead<StopBits>(configuration, nameof(StopBits), out var stopBits))
            StopBits = stopBits;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(StopBits), StopBits.ToString());

        if (TryRead<Parity>(configuration, nameof(Parity), out var parity))
            Parity = parity;
        else
            Logger.LogDefaultSetting(LoggerName, nameof(Parity), Parity.ToString());
    }

    private ILogger<ServiceSettings> Logger { get; }

    public string Message { get; set; } = string.Empty;

    public IPAddress ServerIP { get; set; } = IPAddress.Any;

    public int ServerPort { get; set; } = Constants.DefaultServerPort;

    public string PortName { get; set; } = string.Empty;

    public int BaudRate { get; set; } = Constants.DefaultBaudRate;

    public int DataBits { get; set; } = Constants.DefaultDataBits;

    public StopBits StopBits { get; set; } = Constants.DefaultStopBits;

    public Parity Parity { get; set; } = Constants.DefaultParity;

    /// <summary>
    /// We use this to try and support native compilation.
    /// As opposed to using configuration bind to avoid dynamic code emmit.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="configuration"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool TryRead<T>(IConfiguration configuration, string name, [MaybeNullWhen(false)] out T value)
    {
        value = default;
        var stringValue = configuration.GetSection(nameof(ServiceSettings))[name];

        if (stringValue is null)
            return false;

        if (typeof(T) == typeof(string))
        {
            value = (T)(object)stringValue;
            return true;
        }
        else if (typeof(T) == typeof(IPAddress))
        {
            if (IPAddress.TryParse(stringValue, out var ipAddress))
            {
                value = (T)(object)ipAddress;
                return true;
            }
        }
        else if (typeof(T) == typeof(int))
        {
            if (int.TryParse(stringValue, out var intValue))
            {
                value = (T)(object)intValue;
                return true;
            }
        }
        else if (typeof(T) == typeof(Parity))
        {
            if (Enum.TryParse<Parity>(stringValue, true, out var parityValue))
            {
                value = (T)(object)parityValue;
                return true;
            }
        }
        else if (typeof(T) == typeof(StopBits))
        {
            if (Enum.TryParse<StopBits>(stringValue, true, out var stopBits))
            {
                value = (T)(object)stopBits;
                return true;
            }
        }

        return false;
    }
}
