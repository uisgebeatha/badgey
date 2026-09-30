namespace Badgey.Rendering;

/// <summary>
/// Converts logical pixels to the legacy badge's column-grouped bitmap bytes.
/// </summary>
public static class BadgeBitmapPacker
{
    public const int RequiredHeight = 11;
    public const int PixelsPerByte = 8;

    public static byte[] Pack(MonochromeBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (bitmap.Height != RequiredHeight)
        {
            throw new ArgumentException(
                $"Badge bitmaps must be exactly {RequiredHeight} pixels high.",
                nameof(bitmap));
        }

        var byteColumnCount = (bitmap.Width + PixelsPerByte - 1) / PixelsPerByte;
        var packed = new byte[byteColumnCount * RequiredHeight];

        for (var byteColumn = 0; byteColumn < byteColumnCount; byteColumn++)
        {
            for (var y = 0; y < RequiredHeight; y++)
            {
                byte value = 0;

                for (var bit = 0; bit < PixelsPerByte; bit++)
                {
                    var x = (byteColumn * PixelsPerByte) + bit;

                    if (x < bitmap.Width && bitmap[x, y])
                    {
                        value |= (byte)(1 << (7 - bit));
                    }
                }

                packed[(byteColumn * RequiredHeight) + y] = value;
            }
        }

        return packed;
    }
}
