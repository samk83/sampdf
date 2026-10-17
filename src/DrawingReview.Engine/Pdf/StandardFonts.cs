using System.Runtime.CompilerServices;
using System.Text;
using PdfSharp.Pdf;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 글상자·벡터 PDF에 쓰는 글꼴: 영문·숫자는 Helvetica(기본 14 글꼴, 파일에 안 넣음),
/// 그 밖의 글자(한글 등)는 Adobe-Korea1 CID 글꼴 참조(<see cref="KoreanCidFont"/>). 둘 다 글꼴 파일을 넣지 않아 파일이 작다.
/// </summary>
internal static class StandardFonts
{
    public const string HelveticaName = "/Helv";
    public const string KoreanName = "/KR1";

    private static readonly ConditionalWeakTable<PdfDocument, PdfDictionary> Helvetica = new();

    /// <summary>문서에 Helvetica 글꼴 객체를 한 번만 만든다.</summary>
    public static PdfDictionary GetOrCreateHelvetica(PdfDocument doc)
    {
        if (Helvetica.TryGetValue(doc, out var existing)) return existing;
        var font = new PdfDictionary(doc);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", "/Helvetica");
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        PdfValues.AddObject(doc, font);
        Helvetica.Add(doc, font);
        return font;
    }

    /// <summary>Helvetica로 쓸 수 있는 글자인지 (WinAnsi 범위).</summary>
    public static bool IsLatin(char ch) => ch >= 32 && ch < 127 || ch >= 160 && ch <= 255;

    /// <summary>글자 폭 (글꼴 크기 1 기준). 한글 등 CID 글꼴 글자는 전각(1.0)으로 본다.</summary>
    public static double Width(char ch)
    {
        if (ch >= 32 && ch < 127) return HelveticaWidths[ch - 32] / 1000.0;
        if (ch >= 160 && ch <= 255) return 0.556;
        return 1.0;
    }

    public static double Width(string s) => s.Sum(Width);

    // Helvetica AFM 폭 (코드 32~126), 1000 단위
    private static readonly int[] HelveticaWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, // space ! " # $ % & ' ( ) * + , - . /
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556, // 0-9 : ; < = > ?
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, // @ A-O
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, // P-Z [ \ ] ^ _
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, // ` a-o
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,      // p-z { | } ~
    ];
}

/// <summary>
/// 한글(및 기타 유니코드) 글자를 페이지 본문·주석 외형에 쓰기 위한 Adobe-Korea1 CID 글꼴 참조 (글꼴 파일은 넣지 않음).
/// 글꼴은 보는 프로그램이 제공한다 (Acrobat: 한국어 글꼴 팩, Chrome/PDFium: 시스템 한글 글꼴).
/// 글자는 UTF-16BE 16진 문자열(&lt;...&gt;)로 쓴다 (인코딩 UniKS-UCS2-H).
/// </summary>
public static class KoreanCidFont
{
    private static readonly ConditionalWeakTable<PdfDocument, PdfDictionary> Fonts = new();

    /// <summary>문서에 글꼴 객체를 한 번만 만들고 돌려준다.</summary>
    public static PdfDictionary GetOrCreate(PdfDocument doc)
    {
        if (Fonts.TryGetValue(doc, out var existing)) return existing;

        var descriptor = new PdfDictionary(doc);
        descriptor.Elements.SetName("/Type", "/FontDescriptor");
        descriptor.Elements.SetName("/FontName", "/HYGoThic-Medium");
        descriptor.Elements.SetInteger("/Flags", 4);
        descriptor.Elements["/FontBBox"] = PdfValues.NumberArray(doc, [-6, -145, 1003, 880]);
        descriptor.Elements.SetInteger("/ItalicAngle", 0);
        descriptor.Elements.SetInteger("/Ascent", 880);
        descriptor.Elements.SetInteger("/Descent", -120);
        descriptor.Elements.SetInteger("/CapHeight", 880);
        descriptor.Elements.SetInteger("/StemV", 93);
        PdfValues.AddObject(doc, descriptor);

        var cidFont = new PdfDictionary(doc);
        cidFont.Elements.SetName("/Type", "/Font");
        cidFont.Elements.SetName("/Subtype", "/CIDFontType0");
        cidFont.Elements.SetName("/BaseFont", "/HYGoThic-Medium");
        var systemInfo = new PdfDictionary(doc);
        systemInfo.Elements["/Registry"] = new PdfString("Adobe");
        systemInfo.Elements["/Ordering"] = new PdfString("Korea1");
        systemInfo.Elements.SetInteger("/Supplement", 2);
        cidFont.Elements["/CIDSystemInfo"] = systemInfo;
        cidFont.Elements.SetReference("/FontDescriptor", descriptor);
        cidFont.Elements.SetInteger("/DW", 1000);
        var widths = new PdfArray(doc);
        widths.Elements.Add(new PdfInteger(1)); widths.Elements.Add(new PdfInteger(95)); widths.Elements.Add(new PdfInteger(500));
        cidFont.Elements["/W"] = widths;
        PdfValues.AddObject(doc, cidFont);

        var type0 = new PdfDictionary(doc);
        type0.Elements.SetName("/Type", "/Font");
        type0.Elements.SetName("/Subtype", "/Type0");
        type0.Elements.SetName("/BaseFont", "/HYGoThic-Medium");
        type0.Elements.SetName("/Encoding", "/UniKS-UCS2-H");
        var descendants = new PdfArray(doc);
        descendants.Elements.Add(PdfValues.Ref(cidFont));
        type0.Elements["/DescendantFonts"] = descendants;
        PdfValues.AddObject(doc, type0);
        Fonts.Add(doc, type0);
        return type0;
    }

    /// <summary>글자를 UTF-16BE 16진 문자열 내용으로 바꾼다 (꺾쇠 제외).</summary>
    public static string Hex(string text) =>
        string.Concat(Encoding.BigEndianUnicode.GetBytes(text).Select(b => b.ToString("X2")));

    /// <summary>페이지 리소스의 /Font 사전에 글꼴을 이름(name, 예 "/KR1")으로 등록한다.</summary>
    public static void AddToPageResources(PdfDocument doc, PdfDictionary page, PdfDictionary font, string name)
    {
        var resources = PdfValues.PageResources(doc, page);
        var fonts = PdfValues.ResourceCategory(doc, resources, "/Font");
        fonts.Elements[name.StartsWith('/') ? name : "/" + name] = PdfValues.Ref(font);
    }
}
