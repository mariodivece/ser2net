﻿namespace Unosquare.Ser2Net;

internal static partial class MessageLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "[{Source}] Service is starting . . .")]
    internal static partial void LogServiceStarting(this ILogger<ServiceHost> logger, string source);

    [LoggerMessage(LogLevel.Information, "[TCP Server] Listening for connections on [{EndPoint}].")]
    internal static partial void LogListenerStarted(this ILogger<ServiceHost> logger, EndPoint endPoint);

    [LoggerMessage(LogLevel.Information, "[TCP Server] Server on [{EndPoint}] is shutting down.")]
    internal static partial void LogListenerShuttingDown(this ILogger<ServiceHost> logger, EndPoint? endPoint);

    [LoggerMessage(LogLevel.Information, "Client [{EndPoint}] Connection accepted.")]
    internal static partial void LogClientAccepted(this ILogger<ServiceHost> logger, EndPoint endPoint);

    [LoggerMessage(LogLevel.Warning, "Client [{EndPoint}] rejected because connection count would exceed {MaxConnections}.")]
    internal static partial void LogConnectionRejectedMax(this ILogger<ServiceHost> logger, EndPoint endPoint, int maxConnections);

    [LoggerMessage(LogLevel.Warning, "Client [{EndPoint}] did not complete the connection.")]
    internal static partial void LogConnectionNotCompleted(this ILogger<ServiceHost> logger, EndPoint endPoint);

    [LoggerMessage(LogLevel.Error, "Client [{EndPoint}] Could not write to network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorWriting(this ILogger<NetworkClient> logger, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Error, "Client [{EndPoint}] Could not read from network stream.\r\n{ErrorMessage}")]
    internal static partial void LogErrorReading(this ILogger<NetworkClient> logger, EndPoint endPoint, string errorMessage);

    [LoggerMessage(LogLevel.Information, "Client [{EndPoint}] Disconneted.")]
    internal static partial void LogClientDisconnected(this ILogger<NetworkClient> logger, EndPoint endPoint);

    [LoggerMessage(LogLevel.Warning, "[Settings] Could not parse setting '{Name}'. Will use default value '{Value}'")]
    internal static partial void LogDefaultSetting(this ILogger<ServiceSettings> logger, string name, string? value);
}
