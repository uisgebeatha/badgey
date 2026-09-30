using HidSharp;

namespace Badgey.Hardware;

internal static class BadgeHidDeviceMatcher
{
    internal const int VendorId = 0x0416;
    internal const int ProductId = 0x5020;
    internal const int UsagePage = 0xFF00;
    internal const int Usage = 1;
    internal const string Manufacturer = "wch.cn";
    internal const string ProductName = "CH583";

    private const uint CombinedUsage = ((uint)UsagePage << 16) | Usage;

    public static bool IsCompatible(HidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        try
        {
            var usages = device
                .GetReportDescriptor()
                .DeviceItems
                .SelectMany(item => item.Usages.GetAllValues());

            return IsCompatible(
                device.VendorID,
                device.ProductID,
                device.GetManufacturer(),
                device.GetProductName(),
                device.GetMaxOutputReportLength(),
                usages);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsCompatible(
        int vendorId,
        int productId,
        string? manufacturer,
        string? productName,
        int maximumOutputReportLength,
        IEnumerable<uint> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);

        return vendorId == VendorId
            && productId == ProductId
            && string.Equals(manufacturer?.Trim(), Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(productName?.Trim(), ProductName, StringComparison.OrdinalIgnoreCase)
            && maximumOutputReportLength == BadgeHidReportFramer.HostReportLength
            && usages.Contains(CombinedUsage);
    }
}
