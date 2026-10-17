using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OpenCvSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 그림(PNG·JPG·TIFF·BMP) → 한 장(여러 장 TIFF는 여러 장)짜리 PDF. 그림을 페이지 전체에 꽉 채운다.
/// 페이지 크기는 그림의 해상도(dpi)로 정하고, 해상도 정보가 없으면 96 dpi로 본다.
/// JPEG은 다시 압축하지 않고 그대로 넣고(DCTDecode), 나머지는 원본 픽셀을 무손실(Flate)로 넣는다.
/// </summary>
internal static class ImagePdfBuilder
{
    private const double DefaultDpi = 96;

    public static byte[] Build(byte[] imageBytes, string fileType)
    {
        var ext = fileType.TrimStart('.').ToLowerInvariant();
        var frames = Decode(imageBytes, ext);
        if (frames.Count == 0) throw new InvalidDataException("The image could not be read.");
        try
        {
            var (dpiX, dpiY) = ReadDpi(imageBytes, ext);
            using var doc = new PdfDocument();
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var page = doc.AddPage();
                double width = frame.Width * 72 / dpiX, height = frame.Height * 72 / dpiY;
                page.Width = XUnit.FromPoint(width);
                page.Height = XUnit.FromPoint(height);

                var image = ext is "jpg" or "jpeg" && frames.Count == 1 && frame.Channels() == 3
                    ? JpegXObject(doc, imageBytes, frame)
                    : RawXObject(doc, frame);
                var resources = PageResources(doc, page);
                ResourceCategory(doc, resources, "/XObject").Elements["/Im0"] = Ref(image);
                page.Contents.AppendContent().CreateStream(Ascii(Invariant($"q {Num(width)} 0 0 {Num(height)} 0 0 cm /Im0 Do Q\n")));
            }
            using var ms = new MemoryStream();
            doc.Save(ms, closeStream: false);
            return ms.ToArray();
        }
        finally
        {
            foreach (var f in frames) f.Dispose();
        }
    }

    private static List<Mat> Decode(byte[] bytes, string ext)
    {
        if (ext is "tif" or "tiff")
        {
            // 여러 장 TIFF는 파일에서만 읽을 수 있다
            var temp = Path.Combine(Path.GetTempPath(), $"sdt-{Guid.NewGuid():N}.tif");
            try
            {
                File.WriteAllBytes(temp, bytes);
                if (Cv2.ImReadMulti(temp, out var mats, ImreadModes.Unchanged) && mats.Length > 0) return mats.ToList();
            }
            finally { try { File.Delete(temp); } catch (IOException) { } }
        }
        var single = Cv2.ImDecode(bytes, ImreadModes.Unchanged);
        return single.Empty() ? [] : [single];
    }

    private static PdfDictionary JpegXObject(PdfDocument doc, byte[] jpeg, Mat frame)
    {
        var dict = Stream(doc, jpeg, compress: false);
        dict.Elements.SetName("/Filter", "/DCTDecode");
        return ImageHeader(dict, frame.Width, frame.Height, "/DeviceRGB");
    }

    private static PdfDictionary RawXObject(PdfDocument doc, Mat frame)
    {
        var (pixels, channels) = Samples(frame);
        var dict = Stream(doc, pixels);
        return ImageHeader(dict, frame.Width, frame.Height, channels == 1 ? "/DeviceGray" : "/DeviceRGB");
    }

    private static PdfDictionary ImageHeader(PdfDictionary dict, int width, int height, string colorSpace)
    {
        dict.Elements.SetName("/Type", "/XObject");
        dict.Elements.SetName("/Subtype", "/Image");
        dict.Elements.SetInteger("/Width", width);
        dict.Elements.SetInteger("/Height", height);
        dict.Elements.SetName("/ColorSpace", colorSpace);
        dict.Elements.SetInteger("/BitsPerComponent", 8);
        return dict;
    }

    /// <summary>8비트 회색(1채널) 또는 RGB(3채널) 픽셀 바이트. 투명(알파)은 흰 배경에 합성한다.</summary>
    private static (byte[] Pixels, int Channels) Samples(Mat src)
    {
        Mat m = src;
        if (m.Depth() != MatType.CV_8U)
        {
            m = new Mat();
            src.ConvertTo(m, MatType.CV_8U, src.Depth() == MatType.CV_16U ? 1 / 257.0 : 1.0);
        }
        try
        {
            int ch = m.Channels(), w = m.Width, h = m.Height, rowBytes = w * ch;
            var raw = new byte[rowBytes * h];
            for (int y = 0; y < h; y++) Marshal.Copy(m.Ptr(y), raw, y * rowBytes, rowBytes);
            if (ch == 1) return (raw, 1);

            var outChannels = ch == 2 ? 1 : 3;
            var result = new byte[w * h * outChannels];
            for (int i = 0, o = 0; i < raw.Length; i += ch, o += outChannels)
            {
                if (ch == 2) { result[o] = Blend(raw[i], raw[i + 1]); continue; }
                var a = ch == 4 ? raw[i + 3] : (byte)255;
                result[o] = Blend(raw[i + 2], a);     // OpenCV는 BGR 순서
                result[o + 1] = Blend(raw[i + 1], a);
                result[o + 2] = Blend(raw[i], a);
            }
            return (result, outChannels);
        }
        finally
        {
            if (m != src) m.Dispose();
        }
    }

    private static byte Blend(byte value, byte alpha) =>
        alpha == 255 ? value : (byte)((value * alpha + 255 * (255 - alpha) + 127) / 255);

    // ---- 해상도(dpi) 읽기 ----

    private static (double X, double Y) ReadDpi(byte[] bytes, string ext)
    {
        try
        {
            var dpi = ext switch
            {
                "png" => PngDpi(bytes),
                "jpg" or "jpeg" => JpegDpi(bytes),
                _ => null,
            };
            if (dpi is { X: > 1, Y: > 1 } d) return d;
        }
        catch (Exception) { /* 해상도 정보가 깨졌으면 기본값을 쓴다 */ }
        return (DefaultDpi, DefaultDpi);
    }

    private static (double X, double Y)? PngDpi(byte[] b)
    {
        int pos = 8;
        while (pos + 8 <= b.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(b, pos + 4, 4);
            if (type == "pHYs" && length >= 9 && pos + 8 + 9 <= b.Length)
            {
                var x = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos + 8));
                var y = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(pos + 12));
                return b[pos + 16] == 1 ? (x * 0.0254, y * 0.0254) : null; // 단위 1 = 미터당 픽셀
            }
            if (type == "IDAT" || type == "IEND") return null;
            pos += 12 + length;
        }
        return null;
    }

    private static (double X, double Y)? JpegDpi(byte[] b)
    {
        int pos = 2;
        while (pos + 4 <= b.Length && b[pos] == 0xFF)
        {
            var marker = b[pos + 1];
            if (marker == 0xD8 || marker == 0x01 || marker >= 0xD0 && marker <= 0xD7) { pos += 2; continue; }
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 2));
            if (marker == 0xE0 && length >= 16 && pos + 2 + length <= b.Length
                && b[pos + 4] == 'J' && b[pos + 5] == 'F' && b[pos + 6] == 'I' && b[pos + 7] == 'F')
            {
                var units = b[pos + 11];
                double x = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 12)), y = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 14));
                return units switch { 1 => (x, y), 2 => (x * 2.54, y * 2.54), _ => null };
            }
            if (marker == 0xDA) return null; // 영상 데이터 시작
            pos += 2 + length;
        }
        return null;
    }
}
