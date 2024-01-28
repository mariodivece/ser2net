namespace Unosquare.Ser2Net;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "[{Source,-14}] Service is starting . . .")]
    internal static partial void LogServiceStarting(this ILogger<RootWorkerService> logger, string source);

    [LoggerMessage(LogLevel.Critical, "[{Source,-14}] Service exited because of an unhandled exception")]
    internal static partial void LogServiceError(this ILogger<RootWorkerService> logger, string source, Exception ex);

    [LoggerMessage(LogLevel.Warning, "[Connection    ][{ConnectionIndex}] Could not parse setting '{Name}'. Will use default value '{Value}'")]
    internal static partial void LogDefaultSetting(this ILogger<ConnectionSettings> logger, int connectionIndex, string name, string? value);

    [LoggerMessage(LogLevel.Information, "[Connection    ][{ConnectionIndex}] TCP Listener = {ServerIp}:{ServerPort}, Serial = {PortName} @ {BaudRate}-{DataBits}-{Parity}-{StopBits}")]
    internal static partial void LogConfiguration(this ILogger<ConnectionSettings> logger, int connectionIndex,
        string? serverIp, int? serverPort, string? portName, int? baudRate, int? dataBits, int? stopBits, string? parity);

    [LoggerMessage(LogLevel.Information, "[Connection    ][{ConnectionIndex}] Starting . . .")]
    internal static partial void LogConnectionStarting(this ILogger<ConnectionProxy> logger, int connectionIndex);

    [LoggerMessage(LogLevel.Critical, "[Connection    ][{ConnectionIndex}] Terminated because of an unhandled exception.")]
    internal static partial void LogConnectionError(this ILogger<ConnectionProxy> logger, int connectionIndex, Exception ex);

    [LoggerMessage(LogLevel.Information, "[Network       ][{ConnectionIndex}] Listening for connections on [{EndPoint}].")]
    internal static partial void LogListenerStarted(this ILogger<NetServer> logger, int connectionIndex, EndPoint endPoint);

    [LoggerMessage(LogLevel.Critical, "[Network       ][{ConnectionIndex}] Server on [{EndPoint}] irrecoverably failed.\r\n{Message}")]
    internal static partial void LogListenerFailed(this ILogger<NetServer> logger, int connectionIndex, EndPoint? endPoint, string message, Exception ex);

    [LoggerMessage(LogLevel.Critical, "[Network       ][{ConnectionIndex}] Server on [{EndPoint}] failed to listen for connections.\r\n{Message}")]
    internal static partial void LogListenerLoopFailed(this ILogger<NetServer> logger, int connectionIndex, EndPoint? endPoint, string message, Exception ex);

    [LoggerMessage(LogLevel.Information, "[Network       ][{ConnectionIndex}] Server on [{EndPoint}] is shutting down.")]
    internal static partial void LogListenerShuttingDown(this ILogger<NetServer> logger, int connectionIndex, EndPoint? endPoint);

    [LoggerMessage(LogLevel.Information, "[Network       ][{ConnectionIndex}] Client [{EndPoint}] Connection accepted.")]
    internal static partial void LogClientAccepted(this ILogger<NetServer> logger, int connectionIndex, EndPoint endPoint);

    [LoggerMessage(LogLevel.Warning, "[Network       ][{ConnectionIndex}] Client [{EndPoint}] rejected because connection count would exceed {MaxConnections}.")]
    internal static partial void LogConnectionRejectedMax(this ILogger<NetServer> logger, int connectionIndex, EndPoint endPoint, int maxConnections);

    [LoggerMessage(LogLevel.Warning, "[Network       ][{ConnectionIndex}] Client [{EndPoint}] did not complete the connection.")]
    internal static partial void LogConnectionNotCompleted(this ILogger<NetServer> logger, int connectionIndex, EndPoint endPoint);

    [LoggerMessage(LogLevel.Error, "[Network       ][{ConnectionIndex}] [{EndPoint}] Could not write to network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorWriting(this ILogger<NetworkClient> logger, int connectionIndex, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Error, "[Network       ][{ConnectionIndex}] [{EndPoint}] Could not read from network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorReading(this ILogger<NetworkClient> logger, int connectionIndex, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Information, "[Network       ][{ConnectionIndex}] [{EndPoint}] Disconneted.")]
    internal static partial void LogClientDisconnected(this ILogger<NetworkClient> logger, int connectionIndex, EndPoint endPoint);

    [LoggerMessage(LogLevel.Information, "[Serial        ][{ConnectionIndex}] {PortName} disconnected.")]
    internal static partial void LogPortDisconnected(this ILogger<SerialPortBroker> logger, int connectionIndex, string portName);

    [LoggerMessage(LogLevel.Information, "[Serial        ][{ConnectionIndex}] Broker has stopped.")]
    internal static partial void LogBrokerStopped(this ILogger<SerialPortBroker> logger, int connectionIndex);

    [LoggerMessage(LogLevel.Information, "[Serial        ][{ConnectionIndex}] {PortName} Connection established.")]
    internal static partial void LogPortConnected(this ILogger<SerialPortBroker> logger, int connectionIndex, string portName);

    [LoggerMessage(LogLevel.Debug, "[Serial        ][{ConnectionIndex}] {PortName} Attempting connection.")]
    internal static partial void LogAttemptingConnection(this ILogger<SerialPortBroker> logger, int connectionIndex, string portName);

    [LoggerMessage(LogLevel.Debug, "[Serial        ][{ConnectionIndex}] {PortName} Connection failed.")]
    internal static partial void LogConnectionFailed(this ILogger<SerialPortBroker> logger, int connectionIndex, string portName);

}
