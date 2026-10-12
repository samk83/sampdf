using System.Runtime.CompilerServices;
using System.Text;
using MuPDF.NET;

namespace DrawingReview.TestData;

/// <summary>
/// 페이지 본문에 한글을 넣는 도우미.
///
/// MuPDF.NET 3.28의 Page.InsertText(fontName: "korea")는 한글을 WinAnsi로 인코딩해 글자가 깨지고,
/// InsertHtmlBox는 한글 글꼴 전체(약 19MB)를 넣는다. 그래서 PDF 표준 방식대로
/// "Adobe-Korea1 CID 글꼴 참조(비포함)"를 직접 만든다. 글꼴은 보는 프로그램이 제공한다
/// (Acrobat: 한국어 글꼴 팩, MuPDF: 내장 대체 글꼴).
/// </summary>
public static class KoreanText
{
    private static readonly ConditionalWeakTable<Document, StrongBox<int>> FontXrefs = new();

    /// <param name="x">회전 전 페이지 기준 왼쪽에서의 거리(pt).</param>
    /// <param name="baselineY">회전 전 페이지 기준 위에서 글자 기준선까지 거리(pt).</param>
    public static void Insert(Document doc, int pageIndex, float x, float baselineY, string text, float fontSize)
    {
        var page = doc[pageIndex];
        int font = GetOrCreateFont(doc);
        AddFontToResources(doc, page.Xref, font);

        var mediaHeight = page.MediaBox.Height;
        var hex = string.Concat(Encoding.BigEndianUnicode.GetBytes(text).Select(b => b.ToString("X2")));
        var content = FormattableString.Invariant(
            $"q BT /KR1 {fontSize} Tf 1 0 0 1 {x} {mediaHeight - baselineY} Tm <{hex}> Tj ET Q");

        int stream = doc.GetNewXref();
        doc.UpdateObject(stream, "<<>>");
        doc.UpdateStream(stream, Encoding.ASCII.GetBytes(content));

        var (kind, value) = doc.XrefGetKey(page.Xref, "Contents");
        var newContents = kind == "array"
            ? value.TrimEnd().TrimEnd(']') + $" {stream} 0 R]"
            : $"[{value} {stream} 0 R]";
        doc.XrefSetKey(page.Xref, "Contents", newContents);
    }

    private static int GetOrCreateFont(Document doc)
    {
        if (FontXrefs.TryGetValue(doc, out var box)) return box.Value;

        int descriptor = doc.GetNewXref();
        doc.UpdateObject(descriptor,
            "<< /Type /FontDescriptor /FontName /HYGoThic-Medium /Flags 4 /FontBBox [-6 -145 1003 880] " +
            "/ItalicAngle 0 /Ascent 880 /Descent -120 /CapHeight 880 /StemV 93 >>");
        int cidFont = doc.GetNewXref();
        doc.UpdateObject(cidFont,
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /HYGoThic-Medium " +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (Korea1) /Supplement 2 >> " +
            $"/FontDescriptor {descriptor} 0 R /DW 1000 /W [1 95 500] >>");
        int type0 = doc.GetNewXref();
        doc.UpdateObject(type0,
            "<< /Type /Font /Subtype /Type0 /BaseFont /HYGoThic-Medium /Encoding /UniKS-UCS2-H " +
            $"/DescendantFonts [{cidFont} 0 R] >>");
        FontXrefs.Add(doc, new StrongBox<int>(type0));
        return type0;
    }

    private static void AddFontToResources(Document doc, int pageXref, int fontXref)
    {
        var (resKind, resValue) = doc.XrefGetKey(pageXref, "Resources");
        int owner = pageXref;
        string prefix = "Resources/";
        if (resKind == "xref")
        {
            owner = ParseXref(resValue);
            prefix = "";
        }
        else if (resKind == "null")
        {
            doc.XrefSetKey(pageXref, "Resources", "<<>>");
        }

        var (fontKind, fontValue) = doc.XrefGetKey(owner, prefix + "Font");
        if (fontKind == "xref")
            doc.XrefSetKey(ParseXref(fontValue), "KR1", $"{fontXref} 0 R");
        else
            doc.XrefSetKey(owner, prefix + "Font/KR1", $"{fontXref} 0 R");
    }

    private static int ParseXref(string reference) => int.Parse(reference.Split(' ')[0]);
}
