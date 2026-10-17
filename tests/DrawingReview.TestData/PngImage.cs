using System.Buffers.Binary;
using System.IO.Compression;

namespace DrawingReview.TestData;

/// <summary>테스트용 PNG 읽기 (8비트 회색·RGB·RGBA, 비인터레이스). 렌더링 결과의 픽셀 색을 확인할 때 쓴다.</summary>
public sealed class PngImage
{
    public int Width { get; }
    public int Height { get; }
    public int Channels { get; }
    private readonly byte[] _pixels;

    private PngImage(int width, int height, int channels, byte[] pixels)
    {
        Width = width; Height = height; Channels = channels; _pixels = pixels;
    }

    /// <summary>픽셀의 RGB (0~255).</summary>
    public (int R, int G, int B) GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) throw new ArgumentOutOfRangeException($"({x}, {y}) is outside {Width}x{Height}");
        var i = (y * Width + x) * Channels;
        return Channels == 1 ? (_pixels[i], _pixels[i], _pixels[i]) : (_pixels[i], _pixels[i + 1], _pixels[i + 2]);
    }

    public static PngImage Decode(byte[] png)
    {
        if (png.Length < 8 || png[0] != 137 || png[1] != 'P') throw new InvalidDataException("Not a PNG file.");
        int width = 0, height = 0, channels = 0, pos = 8;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png.AsSpan(pos + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    if (data[8] != 8 || data[12] != 0) throw new NotSupportedException("Only 8-bit non-interlaced PNG is supported.");
                    channels = data[9] switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new NotSupportedException($"PNG color type {data[9]}") };
                    break;
                case "IDAT": idat.Write(data); break;
            }
            if (type == "IEND") break;
            pos += 12 + length;
        }
        if (width == 0) throw new InvalidDataException("PNG header missing.");

        idat.Position = 0;
        using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        var read = 0;
        while (read < raw.Length)
        {
            var n = inflate.Read(raw, read, raw.Length - read);
            if (n == 0) break;
            read += n;
        }

        var pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var src = y * (stride + 1) + 1;
            var dst = y * stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= channels ? pixels[dst + x - channels] : 0;
                int b = y > 0 ? pixels[dst - stride + x] : 0;
                int c = y > 0 && x >= channels ? pixels[dst - stride + x - channels] : 0;
                int value = raw[src + x];
                value += filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"PNG filter {filter}"),
                };
                pixels[dst + x] = (byte)value;
            }
        }
        return new PngImage(width, height, channels, pixels);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
