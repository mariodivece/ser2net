namespace Unosquare.Ser2Net.Statistics;

internal static class StatisticsExtensions
{
    public static void ReportStatistics(this ILogger logger, string source, int connetionIndex, TransferType op, StatisticsCollector<int> stats, ref long lastReportSampleCount)
    {
        const string MessageTamplate = "[{Source,-14}][{ConnectionIndex}] {OP} Total: {Total,16}    Rate: {Rate,19}";

        var statCount = stats.LifetimeSampleCount;

        if (statCount <= 0 ||
            statCount == lastReportSampleCount ||
            statCount % Constants.ReportSampleCount != 0)
            return;

        var total = stats.LifetimeSamplesSum.ToBits();
        var rate = stats.CurrentNaturalRate.HasValue ? $"{((double)stats.CurrentNaturalRate).ToBits()}/s." : "N/A";
        logger.LogInformation(MessageTamplate, source, connetionIndex, op, total, rate);
    }

    private static string ToBits(this double byteLength)
    {
        const int Kilo = 1000;
        const int Mega = Kilo * 1000;

        var totalBits = Math.Abs(byteLength * 8);

        if (totalBits > Mega)
        {
            totalBits = Math.Round(totalBits / Mega, 2);
            return $"{totalBits:n2} Mbits";
        }
        
        if (totalBits > Kilo)
        {
            totalBits = Math.Round(totalBits / Kilo, 2);
            return $"{totalBits:n2} Kbits";
        }

        return $"{totalBits:n2}  bits";
    }
}

internal enum TransferType
{
    TX,
    RX
}
