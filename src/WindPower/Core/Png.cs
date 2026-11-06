using System;
using System.IO;
using System.IO.Compression;

namespace WindPower.Core;

/// <summary>A decoded picture: 4 bytes per pixel (red, green, blue, alpha), rows from the top of the picture down.</summary>
public sealed record Picture(int Width, int Height, byte[] Rgba);

/// <summary>
/// Decodes the pngs three.js writes: 8 bits per channel, RGB or RGBA, not interlaced, and writes the coloured icons.
/// Done here rather than with Unity's ImageConversion, which the game never calls itself, so it may not survive in
/// the build.
/// </summary>
public static class Png
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static Picture Decode(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature)) throw new FormatException("not a png file");

        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        var at = 8;

        while (at + 8 <= bytes.Length)
        {
            var length = BigEndian(bytes, at);
            var type = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
            var data = at + 8;

            if (length < 0 || data + length > bytes.Length) throw new FormatException("png chunk runs past the end of the file");

            if (type == "IHDR")
            {
                width = BigEndian(bytes, data);
                height = BigEndian(bytes, data + 4);

                var depth = bytes[data + 8];
                var colour = bytes[data + 9];
                var interlace = bytes[data + 12];

                channels = colour switch { 2 => 3, 6 => 4, _ => 0 };

                if (depth != 8 || channels == 0 || interlace != 0) throw new FormatException($"png kind not supported (depth {depth}, colour type {colour}, interlace {interlace})");
            }
            else if (type == "IDAT")
            {
                compressed.Write(bytes, data, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            at = data + length + 4;
        }

        if (width <= 0 || height <= 0) throw new FormatException("png has no header");

        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];

        compressed.Position = 0;

        using (var inflate = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            var read = 0;

            while (read < raw.Length)
            {
                var got = inflate.Read(raw, read, raw.Length - read);

                if (got == 0) throw new FormatException("png pixel data is shorter than its size says");

                read += got;
            }
        }

        var rows = Unfilter(raw, stride, height, channels);
        var rgba = new byte[width * height * 4];

        for (var p = 0; p < width * height; p++)
        {
            rgba[p * 4] = rows[p * channels];
            rgba[p * 4 + 1] = rows[p * channels + 1];
            rgba[p * 4 + 2] = rows[p * channels + 2];
            rgba[p * 4 + 3] = channels == 4 ? rows[p * channels + 3] : (byte)255;
        }

        return new Picture(width, height, rgba);
    }

    /// <summary>Writes the picture as an 8-bit RGBA png, every row unfiltered.</summary>
    public static byte[] Encode(Picture picture)
    {
        var stride = picture.Width * 4;
        var raw = new byte[(stride + 1) * picture.Height];

        for (var y = 0; y < picture.Height; y++) Buffer.BlockCopy(picture.Rgba, y * stride, raw, y * (stride + 1) + 1, stride);

        using var compressed = new MemoryStream();

        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(raw, 0, raw.Length);

        var header = new byte[13];

        WriteBigEndian(header, 0, picture.Width);
        WriteBigEndian(header, 4, picture.Height);
        header[8] = 8;
        header[9] = 6;

        using var file = new MemoryStream();

        file.Write(Signature, 0, Signature.Length);
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", Array.Empty<byte>());

        return file.ToArray();
    }

    private static void Chunk(Stream file, string type, byte[] data)
    {
        var body = new byte[4 + data.Length];
        var length = new byte[4];
        var crc = new byte[4];

        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, body, 0);
        Buffer.BlockCopy(data, 0, body, 4, data.Length);
        WriteBigEndian(length, 0, data.Length);
        WriteBigEndian(crc, 0, (int)Crc32(body));
        file.Write(length, 0, 4);
        file.Write(body, 0, body.Length);
        file.Write(crc, 0, 4);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in bytes)
        {
            crc ^= b;

            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteBigEndian(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    private static byte[] Unfilter(byte[] raw, int stride, int height, int bpp)
    {
        var rows = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var src = y * (stride + 1) + 1;
            var dst = y * stride;
            var up = dst - stride;

            for (var x = 0; x < stride; x++)
            {
                int a = x >= bpp ? rows[dst + x - bpp] : 0;
                int b = y > 0 ? rows[up + x] : 0;
                int c = x >= bpp && y > 0 ? rows[up + x - bpp] : 0;

                var predictor = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new FormatException($"png row filter {filter} is not valid"),
                };

                rows[dst + x] = (byte)(raw[src + x] + predictor);
            }
        }

        return rows;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int BigEndian(byte[] bytes, int at) => bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3];
}
