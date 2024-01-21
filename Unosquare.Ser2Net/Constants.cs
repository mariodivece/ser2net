﻿namespace Unosquare.Ser2Net;

internal static class Constants
{
    public const string SerivceName = "Unosquare Ser2Net Service";

    public const string WindowsLogName = "Application";

    public const int ExitCodeSuccess = 0;

    public const int ExitCodeFailure = 1;

    public static readonly IPAddress DefaultServerIP = IPAddress.Any;

    public const int DefaultServerPort = 20108;

    public const int DefaultBaudRate = 115200;

    public const Parity DefaultParity = Parity.None;

    public const int DefaultDataBits = 8;

    public const StopBits DefaultStopBits = StopBits.One;

    public static readonly EndPoint EmptyEndPoint = new IPEndPoint(IPAddress.Any, 0);

    public static readonly int MaxClientCount = 1;

    public const int DefaultBlockSize = 4096;

    public const string LogsDirectoryName = "log";

    public const string LogsBaseFileName = "us2n-.log";

    public static readonly TimeSpan LogsFileFlushInterval = TimeSpan.FromSeconds(1);

    public const int LogsFileCountMax = 31;

    public const int LongDelayMillisconds = 100;

    public const int ShortDelayMilliseconds = 1;

    public const int BridgeQueueSize = 2048;
}
