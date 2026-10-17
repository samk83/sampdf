using System.Globalization;
using System.Text;
using DrawingReview.Engine.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DrawingReview.TestData;

/// <summary>
/// 테스트용 PDF 페이지에 선·사각형·원·글자를 그리는 작은 도우미 (페이지 내용을 직접 만든다).
/// 좌표는 회전 전 페이지 기준 왼쪽 위 원점(pt, 아래로 증가)이다. Finish가 그때까지 그린 경로를 한 번에 선으로 긋는다.
/// </summary>
public sealed class PdfSketch
{
    private readonly PdfDocument _doc;
    private readonly PdfPage _page;
    private readonly double _height;
    private readonly StringBuilder _path = new();
    private readonly StringBuilder _content = new("q 1 J 1 j\n");
    private bool _usesHelvetica;

    private static readonly double Kappa = 4 * (Math.Sqrt(2) - 1) / 3;

    public PdfSketch(PdfDocument doc, PdfPage page)
    {
        _doc = doc;
        _page = page;
        _height = page.Height.Point;
    }

    /// <summary>새 페이지를 만든다 (MediaBox [0 0 width height]).</summary>
    public static PdfPage NewPage(PdfDocument doc, double width, double height)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(width);
        page.Height = XUnit.FromPoint(height);
        return page;
    }

    public PdfSketch DrawLine(double x0, double y0, double x1, double y1)
    {
        _path.Append(F($"{x0} {_height - y0} m {x1} {_height - y1} l\n"));
        return this;
    }

    public PdfSketch DrawRect(double x0, double y0, double x1, double y1)
    {
        _path.Append(F($"{Math.Min(x0, x1)} {_height - Math.Max(y0, y1)} {Math.Abs(x1 - x0)} {Math.Abs(y1 - y0)} re\n"));
        return this;
    }

    public PdfSketch DrawCircle(double cx, double cy, double r) => DrawOval(cx - r, cy - r, cx + r, cy + r);

    public PdfSketch DrawOval(double x0, double y0, double x1, double y1)
    {
        double cx = (x0 + x1) / 2, cy = _height - (y0 + y1) / 2, rx = Math.Abs(x1 - x0) / 2, ry = Math.Abs(y1 - y0) / 2;
        double kx = rx * Kappa, ky = ry * Kappa;
        _path.Append(F($"{cx + rx} {cy} m\n"));
        _path.Append(F($"{cx + rx} {cy + ky} {cx + kx} {cy + ry} {cx} {cy + ry} c\n"));
        _path.Append(F($"{cx - kx} {cy + ry} {cx - rx} {cy + ky} {cx - rx} {cy} c\n"));
        _path.Append(F($"{cx - rx} {cy - ky} {cx - kx} {cy - ry} {cx} {cy - ry} c\n"));
        _path.Append(F($"{cx + kx} {cy - ry} {cx + rx} {cy - ky} {cx + rx} {cy} c\n"));
        return this;
    }

    /// <summary>지금까지 그린 경로를 선으로 긋는다. dashes 예: "[6 3] 0".</summary>
    public PdfSketch Finish(double[] color, double width, string? dashes = null)
    {
        if (_path.Length == 0) return this;
        _content.Append(F($"q {color[0]} {color[1]} {color[2]} RG {width} w "));
        if (dashes != null) _content.Append(dashes).Append(" d ");
        _content.Append('\n').Append(_path).Append("S Q\n");
        _path.Clear();
        return this;
    }

    /// <summary>영문 글자 (Helvetica). (x, baselineY)는 글자 기준선의 시작점.</summary>
    public PdfSketch InsertText(double x, double baselineY, string text, double fontSize)
    {
        _usesHelvetica = true;
        _content.Append(F($"BT /F1 {fontSize} Tf 0 g 1 0 0 1 {x} {_height - baselineY} Tm ({Escape(text)}) Tj ET\n"));
        return this;
    }

    /// <summary>내용을 페이지에 붙인다. 이후에는 이 객체를 더 쓰지 않는다.</summary>
    public void Commit()
    {
        _content.Append("Q\n");
        var stream = _page.Contents.AppendContent();
        stream.CreateStream(Encoding.ASCII.GetBytes(_content.ToString()));
        if (_content.Length > 512) stream.Stream.Zip();
        if (_usesHelvetica) KoreanCidFont.AddToPageResources(_doc, _page, Helvetica(_doc), "F1");
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PdfDocument, PdfDictionary> HelveticaFonts = new();

    public static PdfDictionary Helvetica(PdfDocument doc)
    {
        if (HelveticaFonts.TryGetValue(doc, out var existing)) return existing;
        var font = new PdfDictionary(doc);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", "/Helvetica");
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        doc.Internals.AddObject(font);
        HelveticaFonts.Add(doc, font);
        return font;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string F(FormattableString s) => string.Format(CultureInfo.InvariantCulture, s.Format,
        s.GetArguments().Select(a => a is double d ? d.ToString("0.###", CultureInfo.InvariantCulture) : a is float f ? f.ToString("0.###", CultureInfo.InvariantCulture) : a).ToArray());
}
