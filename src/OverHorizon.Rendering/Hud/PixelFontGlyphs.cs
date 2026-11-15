namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Clean-room 8x8 monospace bitmap glyph generator producing a 128x64 RGBA8 GPU atlas
/// for crisp pixel-art menus, HUD labels, F3 debug telemetry, chat, and floating player nametags.
/// </summary>
public static class PixelFontGlyphs
{
    /// <summary>Width of the generated font atlas in pixels (<c>16 * 8 = 128</c>).</summary>
    public const int AtlasWidth = 128;

    /// <summary>Height of the generated font atlas in pixels (<c>8 * 8 = 64</c>).</summary>
    public const int AtlasHeight = 64;

    /// <summary>Width and height of each glyph cell in pixels.</summary>
    public const int GlyphSize = 8;

    /// <summary>Glyph index (<c>127</c>) reserved as an opaque pure-white tile for solid UI rectangles.</summary>
    public const int SolidWhiteCellIndex = 127;

    // 95 printable ASCII glyphs (32..126), 8 rows x 8 bits per ulong (MSB = top-left pixel).
    private static readonly ulong[] Ascii32To126 =
    [
        0x0000000000000000UL, // 32 ' '
        0x183C3C1818001800UL, // 33 '!'
        0x6C6C240000000000UL, // 34 '"'
        0x6C6CFE6CFE6C6C00UL, // 35 '#'
        0x183E603C067C1800UL, // 36 '$'
        0x00C6CC183066C600UL, // 37 '%'
        0x386C3876DCCE7600UL, // 38 '&'
        0x1818300000000000UL, // 39 '''
        0x0C18303030180C00UL, // 40 '('
        0x30180C0C0C183000UL, // 41 ')'
        0x00663CFF3C660000UL, // 42 '*'
        0x0018187E18180000UL, // 43 '+'
        0x0000000000181830UL, // 44 ','
        0x0000007E00000000UL, // 45 '-'
        0x0000000000181800UL, // 46 '.'
        0x060C183060C08000UL, // 47 '/'
        0x3C66CE76E6663C00UL, // 48 '0'
        0x1838181818187E00UL, // 49 '1'
        0x3C66061C30667E00UL, // 50 '2'
        0x3C66061C06663C00UL, // 51 '3'
        0x0C1C3C6CFE0C0C00UL, // 52 '4'
        0x7E607C0606663C00UL, // 53 '5'
        0x1C30607C66663C00UL, // 54 '6'
        0x7E66060C18181800UL, // 55 '7'
        0x3C66663C66663C00UL, // 56 '8'
        0x3C66663E060C3800UL, // 57 '9'
        0x0018180000181800UL, // 58 ':'
        0x0018180000181830UL, // 59 ';'
        0x0C18306030180C00UL, // 60 '<'
        0x00007E00007E0000UL, // 61 '='
        0x30180C060C183000UL, // 62 '>'
        0x3C66060C18001800UL, // 63 '?'
        0x3C666E6E60623C00UL, // 64 '@'
        0x183C66667E666600UL, // 65 'A'
        0xFC66667C6666FC00UL, // 66 'B'
        0x3C66C0C0C0663C00UL, // 67 'C'
        0xF86C6666666CF800UL, // 68 'D'
        0xFEC0C0F8C0C0FE00UL, // 69 'E'
        0xFEC0C0F8C0C0C000UL, // 70 'F'
        0x3C66C0CE66663E00UL, // 71 'G'
        0x6666667E66666600UL, // 72 'H'
        0x7E18181818187E00UL, // 73 'I'
        0x1E0C0C0C6C6C3800UL, // 74 'J'
        0x666C7870786C6600UL, // 75 'K'
        0xC0C0C0C0C0C0FE00UL, // 76 'L'
        0xC6EEFEFED6C6C600UL, // 77 'M'
        0xC6E6F6DECEC6C600UL, // 78 'N'
        0x3C66C3C3C3663C00UL, // 79 'O'
        0xFC66667C60606000UL, // 80 'P'
        0x3C66C3C3CF6C3600UL, // 81 'Q'
        0xFC66667C6C666600UL, // 82 'R'
        0x3C66603C06663C00UL, // 83 'S'
        0xFE18181818181800UL, // 84 'T'
        0x6666666666663C00UL, // 85 'U'
        0x66666666663C1800UL, // 86 'V'
        0xC6C6C6D6FE6C4400UL, // 87 'W'
        0xC66C38386CC6C600UL, // 88 'X'
        0x6666663C18181800UL, // 89 'Y'
        0xFE060C183060FE00UL, // 90 'Z'
        0x3C30303030303C00UL, // 91 '['
        0xC06030180C060200UL, // 92 '\'
        0x3C0C0C0C0C0C3C00UL, // 93 ']'
        0x10386CC600000000UL, // 94 '^'
        0x00000000000000FEUL, // 95 '_'
        0x30180C0000000000UL, // 96 '`'
        0x00003C063E663E00UL, // 97 'a'
        0x60607C6666667C00UL, // 98 'b'
        0x00003C6660663C00UL, // 99 'c'
        0x06063E6666663E00UL, // 100 'd'
        0x00003C667E603C00UL, // 101 'e'
        0x1C36307C30303000UL, // 102 'f'
        0x00003E66663E063CUL, // 103 'g'
        0x60607C6666666600UL, // 104 'h'
        0x1800381818183C00UL, // 105 'i'
        0x06000E060666663CUL, // 106 'j'
        0x6060666C786C6600UL, // 107 'k'
        0x3818181818183C00UL, // 108 'l'
        0x0000ECFED6C6C600UL, // 109 'm'
        0x00007C6666666600UL, // 110 'n'
        0x00003C6666663C00UL, // 111 'o'
        0x00007C66667C6060UL, // 112 'p'
        0x00003E66663E0606UL, // 113 'q'
        0x00007C6660606000UL, // 114 'r'
        0x00003E603C067C00UL, // 115 's'
        0x30307C3030361C00UL, // 116 't'
        0x0000666666663E00UL, // 117 'u'
        0x00006666663C1800UL, // 118 'v'
        0x0000C6C6D6FE6C00UL, // 119 'w'
        0x0000663C183C6600UL, // 120 'x'
        0x00006666663E063CUL, // 121 'y'
        0x00007E0C18307E00UL, // 122 'z'
        0x0E18187018180E00UL, // 123 '{'
        0x1818181818181800UL, // 124 '|'
        0x7018180E18187000UL, // 125 '}'
        0x76DC000000000000UL, // 126 '~'
    ];

    /// <summary>
    /// Maps a Unicode character (ASCII or basic Cyrillic transliteration fallback) to its 7-bit atlas glyph index.
    /// </summary>
    /// <param name="ch">Input character.</param>
    /// <returns>Atlas cell index in <c>[32, 126]</c>.</returns>
    public static int MapCharToCell(char ch)
    {
        if (ch is >= ' ' and <= '~')
        {
            return ch;
        }

        // Clean visual fallback for common Russian letters to similar Latin pixel glyphs so mixed text stays legible.
        return char.ToUpperInvariant(ch) switch
        {
            'А' => 'A',
            'В' => 'B',
            'Е' or 'Ё' => 'E',
            'К' => 'K',
            'М' => 'M',
            'Н' => 'H',
            'О' => 'O',
            'Р' => 'P',
            'С' => 'C',
            'Т' => 'T',
            'У' => 'Y',
            'Х' => 'X',
            'З' => '3',
            _ => '?',
        };
    }

    /// <summary>
    /// Returns the 64-bit 8x8 row bitmap for an ASCII code in <c>[32, 126]</c>.
    /// </summary>
    /// <param name="asciiCode">ASCII character code.</param>
    /// <returns>64-bit 8x8 bitmap.</returns>
    public static ulong GetBitmap(int asciiCode) =>
        asciiCode is >= 32 and <= 126 ? Ascii32To126[asciiCode - 32] : Ascii32To126['?' - 32];

    /// <summary>
    /// Generates the 128x64 RGBA8 GPU font texture, including the solid white tile at <see cref="SolidWhiteCellIndex"/>.
    /// </summary>
    /// <returns>RGBA8 byte array of length <c>128 * 64 * 4 = 32,768</c>.</returns>
    public static byte[] CreateRgbaAtlas()
    {
        var pixels = new byte[AtlasWidth * AtlasHeight * 4];

        for (var code = 32; code <= 126; code++)
        {
            var bitmap = Ascii32To126[code - 32];
            var cellCol = code & 15;
            var cellRow = code >> 4;

            for (var py = 0; py < GlyphSize; py++)
            {
                var rowBits = (byte)((bitmap >> ((7 - py) * 8)) & 0xFFUL);
                for (var px = 0; px < GlyphSize; px++)
                {
                    var on = (rowBits & (1 << (7 - px))) != 0;
                    if (!on)
                    {
                        continue;
                    }

                    var ax = (cellCol * GlyphSize) + px;
                    var ay = (cellRow * GlyphSize) + py;
                    var idx = ((ay * AtlasWidth) + ax) * 4;
                    pixels[idx] = 255;
                    pixels[idx + 1] = 255;
                    pixels[idx + 2] = 255;
                    pixels[idx + 3] = 255;
                }
            }
        }

        // Fill cell 127 (col 15, row 7) with solid opaque white for UI quads.
        const int whiteCol = SolidWhiteCellIndex & 15;
        const int whiteRow = SolidWhiteCellIndex >> 4;
        for (var py = 0; py < GlyphSize; py++)
        {
            for (var px = 0; px < GlyphSize; px++)
            {
                var ax = (whiteCol * GlyphSize) + px;
                var ay = (whiteRow * GlyphSize) + py;
                var idx = ((ay * AtlasWidth) + ax) * 4;
                pixels[idx] = 255;
                pixels[idx + 1] = 255;
                pixels[idx + 2] = 255;
                pixels[idx + 3] = 255;
            }
        }

        return pixels;
    }
}
