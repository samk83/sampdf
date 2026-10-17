using System.IO.Compression;
using System.Text;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.Engine.Pdf;

/// <summary>RGB 24비트 렌더링 결과를 PNG로 만든다 (외부 그림 라이브러리 없이).</summary>
public static class PngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(RenderedImage image) => Encode(image.Width, image.Height, image.Stride, image.Pixels);

    public static byte[] Encode(int width, int height, int stride, byte[] rgb)
    {
        var rowLength = width * 3 + 1;
        var raw = new byte[rowLength * height];
        for (int y = 0; y < height; y++)
            Buffer.BlockCopy(rgb, y * stride, raw, y * rowLength + 1, width * 3); // 줄마다 앞에 필터 종류 0

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(raw);

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BigEndian(width).CopyTo(header, 0);
        BigEndian(height).CopyTo(header, 4);
        header[8] = 8; // 비트 깊이
        header[9] = 2; // 색: RGB
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(BigEndian(data.Length));
        s.Write(typeBytes);
        s.Write(data);
        uint crc = 0xFFFFFFFF;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        s.Write(BigEndian((int)(crc ^ 0xFFFFFFFF)));
    }

    private static byte[] BigEndian(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
