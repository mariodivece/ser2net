namespace Unosquare.Ser2Net;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "[{Source}] Service is starting . . .")]
    internal static partial void LogServiceStarting(this ILogger<MainService> logger, string source);

    [LoggerMessage(LogLevel.Critical, "[{Source}] Service exited because of an unhandled exception")]
    internal static partial void LogServiceError(this ILogger<MainService> logger, string source, Exception ex);

    [LoggerMessage(LogLevel.Information, "[Connection][{ConnectionIndex}] Starting . . .")]
    internal static partial void LogConnectionStarting(this ILogger<ConnectionProxy> logger, int connectionIndex);

    [LoggerMessage(LogLevel.Critical, "[Connection][{ConnectionIndex}] Terminated because of an unhandled exception.")]
    internal static partial void LogConnectionError(this ILogger<ConnectionProxy> logger, int connectionIndex, Exception ex);

    [LoggerMessage(LogLevel.Information, "[{Source}] Listening for connections on [{EndPoint}].")]
    internal static partial void LogListenerStarted(this ILogger<NetServer> logger, string source, EndPoint endPoint);

    [LoggerMessage(LogLevel.Critical, "[{Source}] Server on [{EndPoint}] irrecoverably failed.\r\n{Message}")]
    internal static partial void LogListenerFailed(this ILogger<NetServer> logger, string source, EndPoint? endPoint, string message, Exception ex);

    [LoggerMessage(LogLevel.Critical, "[{Source}] Server on [{EndPoint}] failed to listen for connections.\r\n{Message}")]
    internal static partial void LogListenerLoopFailed(this ILogger<NetServer> logger, string source, EndPoint? endPoint, string message, Exception ex);

    [LoggerMessage(LogLevel.Information, "[{Source}] Server on [{EndPoint}] is shutting down.")]
    internal static partial void LogListenerShuttingDown(this ILogger<NetServer> logger, string source, EndPoint? endPoint);

    [LoggerMessage(LogLevel.Information, "[{Source}] Client [{EndPoint}] Connection accepted.")]
    internal static partial void LogClientAccepted(this ILogger<NetServer> logger, string source, EndPoint endPoint);

    [LoggerMessage(LogLevel.Warning, "[{Source}] Client [{EndPoint}] rejected because connection count would exceed {MaxConnections}.")]
    internal static partial void LogConnectionRejectedMax(this ILogger<NetServer> logger, string source, EndPoint endPoint, int maxConnections);

    [LoggerMessage(LogLevel.Warning, "[{Source}] Client [{EndPoint}] did not complete the connection.")]
    internal static partial void LogConnectionNotCompleted(this ILogger<NetServer> logger, string source, EndPoint endPoint);

    [LoggerMessage(LogLevel.Error, "[{Source}] Client [{EndPoint}] Could not write to network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorWriting(this ILogger<NetworkClient> logger, string source, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Error, "[{Source}] Client [{EndPoint}] Could not read from network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorReading(this ILogger<NetworkClient> logger, string source, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Information, "[{Source}] Client [{EndPoint}] Disconneted.")]
    internal static partial void LogClientDisconnected(this ILogger<NetworkClient> logger, string source, EndPoint endPoint);

    [LoggerMessage(LogLevel.Warning, "[{Source}] Could not parse setting '{Name}'. Will use default value '{Value}'")]
    internal static partial void LogDefaultSetting(this ILogger<ConnectionSettings> logger, string source, string name, string? value);

    [LoggerMessage(LogLevel.Information, "[{Source}] {PortName} disconnected.")]
    internal static partial void LogPortDisconnected(this ILogger<SerialPortBroker> logger, string source, string portName);

    [LoggerMessage(LogLevel.Information, "[{Source}] Broker has stopped.")]
    internal static partial void LogBrokerStopped(this ILogger<SerialPortBroker> logger, string source);

    [LoggerMessage(LogLevel.Information, "[{Source}] {PortName} Connection established.")]
    internal static partial void LogPortConnected(this ILogger<SerialPortBroker> logger, string source, string portName);

    [LoggerMessage(LogLevel.Debug, "[{source}] {PortName} Attempting connection.")]
    internal static partial void LogAttemptingConnection(this ILogger<SerialPortBroker> logger, string source, string portName);

    [LoggerMessage(LogLevel.Debug, "[{source}] {PortName} Connection failed.")]
    internal static partial void LogConnectionFailed(this ILogger<SerialPortBroker> logger, string source, string portName);

    [LoggerMessage(LogLevel.Debug, "[{Source}] {PortName} MESSAGES_GOSE HERE")]
    internal static partial void LogCC4(this ILogger<SerialPortBroker> logger, string source, string portName);

    [LoggerMessage(LogLevel.Information, "[{Source}] {PortName} MESSAGES_GOSE HERE")]
    internal static partial void LogCC5(this ILogger<SerialPortBroker> logger, string source, string portName);

}
