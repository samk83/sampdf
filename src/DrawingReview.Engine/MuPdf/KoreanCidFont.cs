using System.Runtime.CompilerServices;
using MuPDF.NET;

namespace DrawingReview.Engine.MuPdf;

/// <summary>
/// 한글(및 기타 유니코드) 글자를 페이지 본문에 쓰기 위한 Adobe-Korea1 CID 글꼴 참조 (글꼴 파일은 넣지 않음).
///
/// MuPDF.NET 3.28의 Page.InsertText(fontName: "korea")는 한글을 WinAnsi로 인코딩해 글자가 깨지고,
/// InsertHtmlBox는 한글 글꼴 전체(약 19MB)를 넣는다. 그래서 PDF 표준 방식대로 참조만 만든다.
/// 글꼴은 보는 프로그램이 제공한다 (Acrobat: 한국어 글꼴 팩, MuPDF: 내장 대체 글꼴).
/// 글자는 UTF-16BE 16진 문자열(&lt;...&gt;)로 쓴다 (인코딩 UniKS-UCS2-H).
/// </summary>
public static class KoreanCidFont
{
    private static readonly ConditionalWeakTable<Document, StrongBox<int>> FontXrefs = new();

    /// <summary>문서에 글꼴 객체를 한 번만 만들고 그 xref를 돌려준다.</summary>
    public static int GetOrCreate(Document doc)
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

    /// <summary>글자를 UTF-16BE 16진 문자열 내용으로 바꾼다 (꺾쇠 제외).</summary>
    public static string Hex(string text) =>
        string.Concat(System.Text.Encoding.BigEndianUnicode.GetBytes(text).Select(b => b.ToString("X2")));

    /// <summary>페이지 리소스의 /Font 사전에 글꼴을 이름(name)으로 등록한다. 리소스가 간접 객체여도 처리한다.</summary>
    public static void AddToPageResources(Document doc, int pageXref, int fontXref, string name)
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
            doc.XrefSetKey(ParseXref(fontValue), name, $"{fontXref} 0 R");
        else
            doc.XrefSetKey(owner, prefix + "Font/" + name, $"{fontXref} 0 R");
    }

    private static int ParseXref(string reference) => int.Parse(reference.Split(' ')[0]);
}
