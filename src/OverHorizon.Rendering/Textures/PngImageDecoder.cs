using System.Buffers.Binary;
using System.IO.Compression;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Clean-room RFC 2083 PNG decoder supporting 8-bit RGBA, RGB, Grayscale, Grayscale+Alpha, and 1/2/4/8-bit
/// Indexed-Palette (<c>PLTE</c> + <c>tRNS</c>) images with all five PNG scanline reconstruction filters,
/// plus automatic top-frame cropping for vertical sprite strips (e.g. animated fluids).
/// </summary>
public static class PngImageDecoder
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// Decodes a PNG byte buffer into a <c>16x16</c> RGBA8 tile (<c>1,024</c> bytes).
    /// </summary>
    /// <param name="pngBytes">Raw PNG file bytes.</param>
    /// <param name="rgba16x16">Decoded <c>16x16</c> RGBA8 output (<c>1,024</c> bytes) on success.</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecodeTo16x16Rgba(ReadOnlySpan<byte> pngBytes, out byte[] rgba16x16)
    {
        rgba16x16 = [];
        if (!TryDecodeRgba(pngBytes, out var width, out var height, out var fullRgba) || width <= 0 || height <= 0)
        {
            return false;
        }

        // Animated block strips (e.g., 16x512 water_still.png) store square frames stacked vertically.
        var sourceHeight = height > width && height % width == 0 ? width : height;

        var tile = new byte[16 * 16 * 4];
        for (var ty = 0; ty < 16; ty++)
        {
            var sy = Math.Clamp((ty * sourceHeight) / 16, 0, sourceHeight - 1);
            for (var tx = 0; tx < 16; tx++)
            {
                var sx = Math.Clamp((tx * width) / 16, 0, width - 1);
                var srcIdx = ((sy * width) + sx) * 4;
                var dstIdx = ((ty * 16) + tx) * 4;
                tile[dstIdx + 0] = fullRgba[srcIdx + 0];
                tile[dstIdx + 1] = fullRgba[srcIdx + 1];
                tile[dstIdx + 2] = fullRgba[srcIdx + 2];
                tile[dstIdx + 3] = fullRgba[srcIdx + 3];
            }
        }

        rgba16x16 = tile;
        return true;
    }

    /// <summary>
    /// Decodes a non-interlaced PNG image into linear top-to-bottom RGBA8 pixels.
    /// </summary>
    /// <param name="pngBytes">Raw PNG file bytes.</param>
    /// <param name="width">Decoded image width.</param>
    /// <param name="height">Decoded image height.</param>
    /// <param name="rgba">Decoded RGBA8 pixel buffer (<c>width * height * 4</c> bytes).</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecodeRgba(ReadOnlySpan<byte> pngBytes, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = [];

        if (pngBytes.Length < 33 || !pngBytes[..8].SequenceEqual(PngSignature))
        {
            return false;
        }

        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        byte[]? palette = null;
        byte[]? trns = null;
        using var idatStream = new MemoryStream();

        var offset = 8;
        while (offset + 12 <= pngBytes.Length)
        {
            var chunkLen = BinaryPrimitives.ReadInt32BigEndian(pngBytes.Slice(offset, 4));
            if (chunkLen < 0 || offset + 12 + chunkLen > pngBytes.Length)
            {
                return false;
            }

            var type = pngBytes.Slice(offset + 4, 4);
            var data = pngBytes.Slice(offset + 8, chunkLen);
            offset += 12 + chunkLen;

            if (type.SequenceEqual("IHDR"u8))
            {
                if (chunkLen < 13)
                {
                    return false;
                }

                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = data.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                trns = data.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idatStream.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || interlace != 0 || idatStream.Length == 0)
        {
            return false;
        }

        var bitsPerPixel = GetBitsPerPixel(colorType, bitDepth);
        if (bitsPerPixel <= 0)
        {
            return false;
        }

        var strideBytes = ((width * bitsPerPixel) + 7) / 8;
        var bytesPerPixelFilter = Math.Max(1, bitsPerPixel / 8);
        var rawScanlines = new byte[height * (1 + strideBytes)];

        idatStream.Position = 0;
        try
        {
            using var zlib = new ZLibStream(idatStream, CompressionMode.Decompress);
            var totalRead = 0;
            while (totalRead < rawScanlines.Length)
            {
                var read = zlib.Read(rawScanlines, totalRead, rawScanlines.Length - totalRead);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead < rawScanlines.Length)
            {
                return false;
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var unfiltered = UnfilterScanlines(rawScanlines, height, strideBytes, bytesPerPixelFilter);
        if (unfiltered is null)
        {
            return false;
        }

        return ExpandToRgba(unfiltered, width, height, strideBytes, colorType, bitDepth, palette, trns, out rgba);
    }

    private static int GetBitsPerPixel(byte colorType, byte bitDepth) => (colorType, bitDepth) switch
    {
        (0, 8) => 8,
        (2, 8) => 24,
        (3, 1 or 2 or 4 or 8) => bitDepth,
        (4, 8) => 16,
        (6, 8) => 32,
        _ => 0,
    };

    private static byte[]? UnfilterScanlines(byte[] raw, int height, int stride, int bpp)
    {
        var output = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * (1 + stride);
            var filter = raw[srcRow];
            var dstRow = y * stride;
            var prevRow = (y - 1) * stride;

            for (var x = 0; x < stride; x++)
            {
                var rawByte = raw[srcRow + 1 + x];
                var left = x >= bpp ? output[dstRow + x - bpp] : (byte)0;
                var up = y > 0 ? output[prevRow + x] : (byte)0;
                var upLeft = (y > 0 && x >= bpp) ? output[prevRow + x - bpp] : (byte)0;

                output[dstRow + x] = filter switch
                {
                    0 => rawByte,
                    1 => unchecked((byte)(rawByte + left)),
                    2 => unchecked((byte)(rawByte + up)),
                    3 => unchecked((byte)(rawByte + ((left + up) >> 1))),
                    4 => unchecked((byte)(rawByte + PaethPredictor(left, up, upLeft))),
                    _ => rawByte,
                };
                if (filter > 4)
                {
                    return null;
                }
            }
        }

        return output;
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static bool ExpandToRgba(
        byte[] scanlines,
        int width,
        int height,
        int stride,
        byte colorType,
        byte bitDepth,
        byte[]? palette,
        byte[]? trns,
        out byte[] rgba)
    {
        rgba = new byte[width * height * 4];
        if (colorType == 3 && (palette is null || palette.Length < 3))
        {
            return false;
        }

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < width; x++)
            {
                var dst = ((y * width) + x) * 4;
                switch (colorType)
                {
                    case 6:
                        var s6 = rowOffset + (x * 4);
                        rgba[dst + 0] = scanlines[s6 + 0];
                        rgba[dst + 1] = scanlines[s6 + 1];
                        rgba[dst + 2] = scanlines[s6 + 2];
                        rgba[dst + 3] = scanlines[s6 + 3];
                        break;

                    case 2:
                        var s2 = rowOffset + (x * 3);
                        var r2 = scanlines[s2 + 0];
                        var g2 = scanlines[s2 + 1];
                        var b2 = scanlines[s2 + 2];
                        var keyTransparent = trns is { Length: >= 6 } && trns[1] == r2 && trns[3] == g2 && trns[5] == b2;
                        rgba[dst + 0] = r2;
                        rgba[dst + 1] = g2;
                        rgba[dst + 2] = b2;
                        rgba[dst + 3] = keyTransparent ? (byte)0 : (byte)255;
                        break;

                    case 0:
                        var g0 = scanlines[rowOffset + x];
                        rgba[dst + 0] = g0;
                        rgba[dst + 1] = g0;
                        rgba[dst + 2] = g0;
                        rgba[dst + 3] = (trns is { Length: >= 2 } && trns[1] == g0) ? (byte)0 : (byte)255;
                        break;

                    case 4:
                        var s4 = rowOffset + (x * 2);
                        rgba[dst + 0] = scanlines[s4];
                        rgba[dst + 1] = scanlines[s4];
                        rgba[dst + 2] = scanlines[s4];
                        rgba[dst + 3] = scanlines[s4 + 1];
                        break;

                    case 3:
                        var palIdx = ReadBitIndex(scanlines, rowOffset, x, bitDepth);
                        var pOff = palIdx * 3;
                        if (pOff + 2 >= palette!.Length)
                        {
                            break;
                        }

                        rgba[dst + 0] = palette[pOff + 0];
                        rgba[dst + 1] = palette[pOff + 1];
                        rgba[dst + 2] = palette[pOff + 2];
                        rgba[dst + 3] = (trns is not null && palIdx < trns.Length) ? trns[palIdx] : (byte)255;
                        break;
                }
            }
        }

        return true;
    }

    private static int ReadBitIndex(byte[] scanlines, int rowOffset, int x, byte bitDepth)
    {
        if (bitDepth == 8)
        {
            return scanlines[rowOffset + x];
        }

        var pixelsPerByte = 8 / bitDepth;
        var byteVal = scanlines[rowOffset + (x / pixelsPerByte)];
        var shift = (pixelsPerByte - 1 - (x % pixelsPerByte)) * bitDepth;
        var mask = (1 << bitDepth) - 1;
        return (byteVal >> shift) & mask;
    }
}
