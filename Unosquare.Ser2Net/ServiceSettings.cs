using System.Net;

namespace Unosquare.Ser2Net;

internal class ServiceSettings
{
    public const string SectionName = nameof(ServiceSettings);

    public string Message { get; set; } = string.Empty;

    public string ServerIP { get; set; } = IPAddress.Any.ToString();

    public int ServerPort { get; set; } = Constants.DefaultServerPort;

    public string PortName { get; set; } = string.Empty;

    public int BaudRate { get; set; } = Constants.DefaultBaudRate;
}
