using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

var message = string.Join(' ', args);

if (string.IsNullOrWhiteSpace(message))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project .\\tools\\Badgey.DeviceTest\\Badgey.DeviceTest.csproj -- \"message text\"");
    return 2;
}

using var transport = new BadgeHidTransport();

try
{
    Console.WriteLine("Detecting the confirmed LSLED CH583 badge...");
    var found = transport.IsCompatibleBadgePresent();
    Console.WriteLine(found ? "Badge found." : "Badge not found.");

    Console.WriteLine("Opening badge...");
    transport.Open();
    Console.WriteLine("Badge opened.");

    var renderer = new WindowsTextRenderer();
    var bitmap = renderer.Render(message);
    var packedBitmap = BadgeBitmapPacker.Pack(bitmap);
    var upload = BadgeProtocol.CreateUpload(
        packedBitmap,
        BadgeDisplayMode.ScrollLeft,
        speed: 4);

    Console.WriteLine($"Rendered size: {bitmap.Width} x {bitmap.Height} pixels.");
    Console.WriteLine($"Protocol size: {upload.Length} bytes.");
    Console.WriteLine("Sending in scroll-left mode at speed 4...");
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
