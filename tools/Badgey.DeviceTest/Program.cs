using Badgey.Hardware;
using Badgey.Protocol;

// A pre-packed 40 x 11 hollow rectangle: five eight-pixel byte-columns,
// with one byte per display row. It leaves a two-pixel margin on each side
// of the 44 x 11 badge when shown in static-centred mode.
byte[] testPattern =
[
    0xFF, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0xFF,
    0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
    0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
    0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
    0xFF, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0xFF,
];

using var transport = new BadgeHidTransport();

try
{
    Console.WriteLine("Detecting the confirmed LSLED CH583 badge...");
    var found = transport.IsCompatibleBadgePresent();
    Console.WriteLine(found ? "Badge found." : "Badge not found.");

    Console.WriteLine("Opening badge...");
    transport.Open();
    Console.WriteLine("Badge opened.");

    var upload = BadgeProtocol.CreateUpload(
        testPattern,
        BadgeDisplayMode.StaticCentered,
        speed: 1);

    Console.WriteLine(
        $"Sending a static 40 x 11 hollow rectangle ({upload.Length} protocol bytes)...");
    transport.Write(upload);
    Console.WriteLine("Upload succeeded.");

    return 0;
}
catch (BadgeHidException exception)
{
    Console.Error.WriteLine($"{exception.GetType().FullName}: {exception.Message}");

    if (exception.InnerException is not null)
    {
        Console.Error.WriteLine(
            $"Caused by {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
    }

    return 1;
}
finally
{
    transport.Close();
    Console.WriteLine("Badge connection closed.");
}
