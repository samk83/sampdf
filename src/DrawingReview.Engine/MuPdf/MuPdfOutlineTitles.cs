using System.Text;
using System.Text.RegularExpressions;
using MuPDF.NET;

namespace DrawingReview.Engine.MuPdf;

/// <summary>
/// 책갈피(목차) 제목을 PDF 객체에서 직접 읽는다.
///
/// MuPDF.NET 3.28.2.4의 GetToc 제목은 인코딩 지정이 없는 통로(fz_outline title)로 넘어와
/// 한국어 Windows에서 한글이 깨진다 (MuPdfUtf8Fix로 고칠 수 없는 경로).
/// 그래서 XrefObject(ascii: true)로 영문자만으로 된 객체 원문을 받아 /Title 문자열을 직접 해석한다.
/// 순서는 GetToc과 같은 깊이 우선(부모 → 자식 → 다음 형제)이다.
/// </summary>
internal static class MuPdfOutlineTitles
{
    private const int MaxItems = 100_000;

    /// <summary>목차 제목들 (깊이 우선 순서). 목차가 없거나 읽을 수 없으면 null.</summary>
    public static List<string>? Read(Document doc)
    {
        try
        {
            var (kind, value) = doc.XrefGetKey(doc.PdfCatalog, "Outlines");
            if (kind != "xref") return null;
            var titles = new List<string>();
            var visited = new HashSet<int>();
            Walk(doc, Child(doc, Ref(value), "First"), titles, visited);
            return titles;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Walk(Document doc, int item, List<string> titles, HashSet<int> visited)
    {
        // 형제는 반복으로, 자식은 재귀로 (손상된 순환 참조는 visited로 막는다)
        while (item > 0 && visited.Add(item) && titles.Count < MaxItems)
        {
            titles.Add(Title(doc.XrefObject(item, compressed: false, ascii: true)));
            Walk(doc, Child(doc, item, "First"), titles, visited);
            item = Child(doc, item, "Next");
        }
    }

    private static int Child(Document doc, int xref, string key)
    {
        if (xref <= 0) return 0;
        var (kind, value) = doc.XrefGetKey(xref, key);
        return kind == "xref" ? Ref(value) : 0;
    }

    private static int Ref(string reference) => int.Parse(reference.Split(' ')[0]);

    private static readonly Regex TitleStart = new(@"/Title\s*([(<])", RegexOptions.Compiled);

    /// <summary>객체 원문에서 /Title 값을 찾아 글자로 바꾼다. 없으면 빈 문자열.</summary>
    public static string Title(string objectSource)
    {
        var m = TitleStart.Match(objectSource);
        if (!m.Success) return "";
        var start = m.Groups[1].Index;
        var bytes = objectSource[start] == '<' ? HexBytes(objectSource, start) : LiteralBytes(objectSource, start);
        return Decode(bytes);
    }

    /// <summary>PDF 문자열 바이트 → 글자: UTF-16BE(FE FF), UTF-8(EF BB BF), 그 밖은 PDFDocEncoding(라틴 문자로 근사).</summary>
    public static string Decode(byte[] b)
    {
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
        return Encoding.Latin1.GetString(b);
    }

    private static byte[] HexBytes(string s, int start)
    {
        var end = s.IndexOf('>', start);
        var hex = new string(s[(start + 1)..(end < 0 ? s.Length : end)].Where(Uri.IsHexDigit).ToArray());
        if (hex.Length % 2 == 1) hex += "0"; // PDF 규칙: 홀수면 마지막에 0을 붙인다
        return Convert.FromHexString(hex);
    }

    private static byte[] LiteralBytes(string s, int start)
    {
        var bytes = new List<byte>();
        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '(') { if (depth++ > 0) bytes.Add((byte)c); continue; }
            if (c == ')') { if (--depth == 0) break; bytes.Add((byte)c); continue; }
            if (c != '\\') { bytes.Add((byte)c); continue; }
            if (++i >= s.Length) break;
            var e = s[i];
            switch (e)
            {
                case 'n': bytes.Add(10); break;
                case 'r': bytes.Add(13); break;
                case 't': bytes.Add(9); break;
                case 'b': bytes.Add(8); break;
                case 'f': bytes.Add(12); break;
                case '\r': if (i + 1 < s.Length && s[i + 1] == '\n') i++; break; // 줄 이어 쓰기
                case '\n': break;
                case >= '0' and <= '7':
                    {
                        int v = 0, n = 0;
                        while (n < 3 && i < s.Length && s[i] >= '0' && s[i] <= '7') { v = v * 8 + (s[i] - '0'); i++; n++; }
                        i--;
                        bytes.Add((byte)v);
                        break;
                    }
                default: bytes.Add((byte)e); break; // \( \) \\ 등
            }
        }
        return bytes.ToArray();
    }
}
