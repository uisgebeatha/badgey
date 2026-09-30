namespace Badgey.Hardware;

public abstract class BadgeHidException : IOException
{
    protected BadgeHidException(string message)
        : base(message)
    {
    }

    protected BadgeHidException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class BadgeDeviceNotFoundException : BadgeHidException
{
    public BadgeDeviceNotFoundException()
        : base("No compatible LSLED CH583 badge was found.")
    {
    }
}

public sealed class BadgeDeviceOpenException : BadgeHidException
{
    internal BadgeDeviceOpenException(string message)
        : base(message)
    {
    }

    internal BadgeDeviceOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class BadgeDeviceWriteException : BadgeHidException
{
    internal BadgeDeviceWriteException(int reportNumber, Exception innerException)
        : base($"Writing HID report {reportNumber} to the badge failed.", innerException)
    {
    }
}
