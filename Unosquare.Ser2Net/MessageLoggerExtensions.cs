namespace Unosquare.Ser2Net;

internal static partial class MessageLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "[{Source}] Service is starting . . .")]
    internal static partial void LogServiceStarting(this ILogger logger, string source);
}
