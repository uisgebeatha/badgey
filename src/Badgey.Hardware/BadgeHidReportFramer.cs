namespace Badgey.Hardware;

internal static class BadgeHidReportFramer
{
    internal const int PayloadLength = 64;
    internal const int HostReportLength = PayloadLength + 1;
    internal const int MaximumProtocolOutputLength = 8192;

    public static IReadOnlyList<byte[]> CreateReports(byte[] protocolOutput)
    {
        ArgumentNullException.ThrowIfNull(protocolOutput);

        if (protocolOutput.Length == 0)
        {
            throw new ArgumentException("Protocol output must not be empty.", nameof(protocolOutput));
        }

        if (protocolOutput.Length % PayloadLength != 0)
        {
            throw new ArgumentException(
                $"Protocol output must be aligned to {PayloadLength}-byte HID payloads.",
                nameof(protocolOutput));
        }

        if (protocolOutput.Length > MaximumProtocolOutputLength)
        {
            throw new ArgumentException(
                $"Protocol output must not exceed {MaximumProtocolOutputLength} bytes.",
                nameof(protocolOutput));
        }

        var reports = new byte[protocolOutput.Length / PayloadLength][];

        for (var reportIndex = 0; reportIndex < reports.Length; reportIndex++)
        {
            var report = new byte[HostReportLength];
            Buffer.BlockCopy(
                protocolOutput,
                reportIndex * PayloadLength,
                report,
                1,
                PayloadLength);
            reports[reportIndex] = report;
        }

        return reports;
    }
}
