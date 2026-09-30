using HidSharp;

namespace Badgey.Hardware;

/// <summary>
/// Discovers and writes to the confirmed LSLED CH583 vendor-defined HID interface.
/// </summary>
public sealed class BadgeHidTransport : IDisposable
{
    public const int VendorId = BadgeHidDeviceMatcher.VendorId;
    public const int ProductId = BadgeHidDeviceMatcher.ProductId;
    public const int UsagePage = BadgeHidDeviceMatcher.UsagePage;
    public const int Usage = BadgeHidDeviceMatcher.Usage;
    public const int PayloadReportLength = BadgeHidReportFramer.PayloadLength;
    public const int HostWriteBufferLength = BadgeHidReportFramer.HostReportLength;
    public const int MaximumProtocolOutputLength = BadgeHidReportFramer.MaximumProtocolOutputLength;

    private readonly object _syncRoot = new();
    private HidStream? _stream;
    private bool _disposed;

    public bool IsOpen
    {
        get
        {
            lock (_syncRoot)
            {
                return _stream is not null;
            }
        }
    }

    public bool IsCompatibleBadgePresent()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return FindCompatibleDevice() is not null;
        }
    }

    public void Open()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_stream is not null)
            {
                return;
            }

            HidDevice? device;

            try
            {
                device = FindCompatibleDevice();
            }
            catch (Exception exception)
            {
                throw new BadgeDeviceOpenException("Searching for a compatible badge failed.", exception);
            }

            if (device is null)
            {
                throw new BadgeDeviceNotFoundException();
            }

            try
            {
                if (!device.TryOpen(out var stream) || stream is null)
                {
                    throw new BadgeDeviceOpenException("The compatible badge could not be opened.");
                }

                _stream = stream;
            }
            catch (BadgeDeviceOpenException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BadgeDeviceOpenException("The compatible badge could not be opened.", exception);
            }
        }
    }

    public void Write(byte[] protocolOutput)
    {
        var reports = BadgeHidReportFramer.CreateReports(protocolOutput);

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_stream is null)
            {
                throw new InvalidOperationException("The badge must be opened before writing.");
            }

            for (var reportIndex = 0; reportIndex < reports.Count; reportIndex++)
            {
                try
                {
                    _stream.Write(reports[reportIndex]);
                }
                catch (Exception exception)
                {
                    throw new BadgeDeviceWriteException(reportIndex + 1, exception);
                }
            }
        }
    }

    public void Close()
    {
        lock (_syncRoot)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _stream?.Dispose();
            _stream = null;
            _disposed = true;
        }
    }

    private static HidDevice? FindCompatibleDevice() =>
        DeviceList.Local
            .GetHidDevices(VendorId, ProductId)
            .FirstOrDefault(BadgeHidDeviceMatcher.IsCompatible);
}
