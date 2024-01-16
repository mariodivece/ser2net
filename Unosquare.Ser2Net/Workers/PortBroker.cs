using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Ser2Net.Workers;

/// <summary>
/// 
/// </summary>
internal class PortBroker : WorkerBase<PortBroker>
{
    public PortBroker(ILogger<PortBroker> logger, ServiceSettings settings, IServiceProvider services)
        : base(logger, settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    private IServiceProvider Services { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var performDelay = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (performDelay)
                await Task.Delay(500, stoppingToken);

            performDelay = false;
            var serialPortNames = SerialPort.GetPortNames();

            // No serial ports detected so far
            if (serialPortNames.Length <= 0) {
                performDelay = true;
                continue;
            }

            var wantedPortName = Settings.PortName;
            if (string.IsNullOrWhiteSpace(wantedPortName))
                wantedPortName = serialPortNames[0];

            // wanted port name not found;
            if (!serialPortNames.Contains(wantedPortName))
            {
                performDelay = true;
                continue;
            }

            // TODO: check if port is in use
            // https://stackoverflow.com/questions/195483/c-sharp-check-if-a-com-serial-port-is-already-open
            throw new NotImplementedException();
        }
    }
}
