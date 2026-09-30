namespace Badgey.Rendering;

/// <summary>
/// Deterministic 11-row glyphs for ASCII symbols that do not rasterize reliably
/// at LED-matrix resolution. Text layout still supplies each glyph's advance.
/// </summary>
internal static class AsciiPixelGlyphs
{
    private static readonly IReadOnlyDictionary<char, PixelGlyph> Glyphs =
        new Dictionary<char, PixelGlyph>
        {
            ['\\'] = Glyph(6, 0,
                0b100000, 0b010000, 0b010000, 0b001000, 0b001000, 0b000100,
                0b000100, 0b000010, 0b000010, 0b000001, 0b000001),
            ['/'] = Glyph(6, 0,
                0b000001, 0b000010, 0b000010, 0b000100, 0b000100, 0b001000,
                0b001000, 0b010000, 0b010000, 0b100000, 0b100000),
            ['_'] = Glyph(5, 10, 0b11111),
            ['-'] = Glyph(5, 5, 0b11111),
            ['='] = Glyph(5, 3, 0b11111, 0, 0, 0b11111),
            ['+'] = Glyph(5, 3, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100),
            ['.'] = Glyph(1, 9, 0b1),
            [','] = Glyph(2, 8, 0b01, 0b01, 0b10),
            [':'] = Glyph(1, 3, 0b1, 0, 0, 0, 0, 0b1),
            [';'] = Glyph(2, 3, 0b01, 0, 0, 0, 0, 0b01, 0b01, 0b10),
            ['\''] = Glyph(1, 0, 0b1, 0b1),
            ['"'] = Glyph(5, 0, 0b01010, 0b01010),
            ['('] = Glyph(4, 0,
                0b0010, 0b0100, 0b0100, 0b1000, 0b1000, 0b1000,
                0b1000, 0b1000, 0b0100, 0b0100, 0b0010),
            [')'] = Glyph(4, 0,
                0b0100, 0b0010, 0b0010, 0b0001, 0b0001, 0b0001,
                0b0001, 0b0001, 0b0010, 0b0010, 0b0100),
            ['['] = Glyph(4, 0,
                0b1110, 0b1000, 0b1000, 0b1000, 0b1000, 0b1000,
                0b1000, 0b1000, 0b1000, 0b1000, 0b1110),
            [']'] = Glyph(4, 0,
                0b0111, 0b0001, 0b0001, 0b0001, 0b0001, 0b0001,
                0b0001, 0b0001, 0b0001, 0b0001, 0b0111),
            ['{'] = Glyph(5, 0,
                0b00110, 0b00100, 0b00100, 0b00100, 0b01000, 0b10000,
                0b01000, 0b00100, 0b00100, 0b00100, 0b00110),
            ['}'] = Glyph(5, 0,
                0b01100, 0b00100, 0b00100, 0b00100, 0b00010, 0b00001,
                0b00010, 0b00100, 0b00100, 0b00100, 0b01100),
            ['<'] = Glyph(5, 1,
                0b00001, 0b00010, 0b00100, 0b01000, 0b10000,
                0b01000, 0b00100, 0b00010, 0b00001),
            ['>'] = Glyph(5, 1,
                0b10000, 0b01000, 0b00100, 0b00010, 0b00001,
                0b00010, 0b00100, 0b01000, 0b10000),
            ['!'] = Glyph(1, 0, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0, 0, 0b1),
            ['?'] = Glyph(5, 0,
                0b01110, 0b10001, 0b00001, 0b00010, 0b00100,
                0b00100, 0, 0, 0b00100),
            ['|'] = Glyph(1, 0, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1, 0b1),
            ['~'] = Glyph(5, 4, 0b01100, 0b10011),
            ['^'] = Glyph(5, 0, 0b00100, 0b01010, 0b10001),
            ['`'] = Glyph(2, 0, 0b10, 0b01),
        };

    public static bool Contains(char value) => Glyphs.ContainsKey(value);

    public static bool TryGetValue(char value, out PixelGlyph glyph) =>
        Glyphs.TryGetValue(value, out glyph);

    private static PixelGlyph Glyph(int width, int top, params ushort[] rows)
    {
        if (width is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (top < 0 || top + rows.Length > BadgeBitmapPacker.RequiredHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(top));
        }

        if (rows.Any(row => row >= (1 << width)))
        {
            throw new ArgumentException("A glyph row contains pixels outside its declared width.", nameof(rows));
        }

        return new PixelGlyph(width, top, rows);
    }
}

internal readonly record struct PixelGlyph(int Width, int Top, ushort[] Rows);
