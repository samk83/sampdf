using System.Globalization;
using System.Text;
using DrawingReview.Engine.Markups;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace DrawingReview.Engine.Pdf;

/// <summary>PDFsharp 객체 모델(사전·배열·숫자·문자열)을 읽고 쓰는 작은 도우미 모음.</summary>
internal static class PdfValues
{
    /// <summary>PDF 안에 쓰는 숫자 표기 (소수 셋째 자리, 문화권 무관).</summary>
    public static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Num(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

    public static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    /// <summary>PDF 날짜 문자열 (D:YYYYMMDDHHmmSS+HH'mm').</summary>
    public static string Date(DateTimeOffset t)
    {
        var offset = t.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        return "D:" + t.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            + $"{sign}{Math.Abs(offset.Hours):00}'{Math.Abs(offset.Minutes):00}'";
    }

    /// <summary>참조면 따라가서 실제 항목을 돌려준다.</summary>
    public static PdfItem? Resolve(PdfItem? item) => item is PdfReference r ? r.Value : item;

    public static PdfDictionary? Dict(PdfItem? item) => Resolve(item) as PdfDictionary;

    public static PdfArray? Array(PdfItem? item) => Resolve(item) as PdfArray;

    public static double? Number(PdfItem? item) => Resolve(item) switch
    {
        PdfReal r => r.Value,
        PdfInteger i => i.Value,
        PdfLongInteger l => l.Value,
        _ => null,
    };

    public static double? Number(PdfDictionary dict, string key) => dict.Elements.ContainsKey(key) ? Number(dict.Elements[key]) : null;

    /// <summary>숫자 배열. 배열이 아니거나 숫자가 아닌 항목이 있으면 null.</summary>
    public static double[]? Numbers(PdfDictionary dict, string key)
    {
        if (!dict.Elements.ContainsKey(key) || Array(dict.Elements[key]) is not { } array) return null;
        var values = new double[array.Elements.Count];
        for (int i = 0; i < values.Length; i++)
        {
            if (Number(array.Elements[i]) is not { } n) return null;
            values[i] = n;
        }
        return values;
    }

    public static string? Name(PdfDictionary dict, string key) =>
        dict.Elements.ContainsKey(key) && Resolve(dict.Elements[key]) is PdfName n ? n.Value : null;

    /// <summary>문자열 값. 없거나 비어 있으면 null.</summary>
    public static string? Text(PdfDictionary dict, string key)
    {
        if (!dict.Elements.ContainsKey(key) || Resolve(dict.Elements[key]) is not PdfString s) return null;
        return string.IsNullOrEmpty(s.Value) ? null : s.Value;
    }

    public static PdfArray NumberArray(PdfDocument doc, IEnumerable<double> values)
    {
        var array = new PdfArray(doc);
        foreach (var v in values) array.Elements.Add(new PdfReal(Math.Round(v, 4)));
        return array;
    }

    public static PdfArray ColorArray(PdfDocument doc, RgbColor c) => NumberArray(doc, [c.R, c.G, c.B]);

    /// <summary>/C, /IC 같은 색 배열 → RGB (회색·CMYK도 변환).</summary>
    public static RgbColor? Color(PdfDictionary dict, string key)
    {
        var c = Numbers(dict, key);
        return c?.Length switch
        {
            3 => new RgbColor(c[0], c[1], c[2]),
            1 => new RgbColor(c[0], c[0], c[0]),
            4 => new RgbColor((1 - c[0]) * (1 - c[3]), (1 - c[1]) * (1 - c[3]), (1 - c[2]) * (1 - c[3])),
            _ => null,
        };
    }

    /// <summary>문자열에 ASCII 밖 글자가 있으면 UTF-16(BOM) 16진 표기로, 아니면 그대로 쓴다. PDFsharp가 알아서 고른다.</summary>
    public static PdfString String(string value) => new(value, NeedsUnicode(value) ? PdfStringEncoding.Unicode : PdfStringEncoding.PDFDocEncoding);

    private static bool NeedsUnicode(string s) => s.Any(ch => ch > 126 || ch < 32 && ch is not ('\n' or '\r' or '\t'));

    /// <summary>스트림 사전을 만들고 내용을 넣는다 (길이가 좀 되면 압축).</summary>
    public static PdfDictionary Stream(PdfDocument doc, byte[] content, bool compress = true)
    {
        var dict = new PdfDictionary(doc);
        dict.CreateStream(content);
        if (compress && content.Length > 256) dict.Stream.Zip();
        AddObject(doc, dict);
        return dict;
    }

    /// <summary>간접 객체로 등록하고 증분 저장 대상으로 기억한다. 엔진 안의 모든 새 객체는 이 함수로 만든다.</summary>
    public static void AddObject(PdfDocument doc, PdfObject obj)
    {
        doc.Internals.AddObject(obj);
        ChangeTracker.For(doc).Touch(obj);
    }

    /// <summary>이미 있는 간접 객체를 바꿨다고 기억한다 (증분 저장 때 다시 쓴다).</summary>
    public static void Touch(PdfDocument doc, PdfObject obj) => ChangeTracker.For(doc).Touch(obj);

    public static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>PDF 문자열 리터럴 안에 넣을 수 있게 이스케이프한다 (Latin-1 범위 글자만).</summary>
    public static string EscapeLiteral(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                default:
                    if (ch < 32 || ch > 255) sb.Append('?');
                    else if (ch > 126) sb.Append('\\').Append(Convert.ToString(ch, 8).PadLeft(3, '0'));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>페이지의 리소스 사전. 페이지에 직접 없고 상위(Pages)에서 물려받는 경우 항목을 복사해 페이지 것으로 만든다.</summary>
    public static PdfDictionary PageResources(PdfDocument doc, PdfDictionary page)
    {
        if (Dict(page.Elements.ContainsKey("/Resources") ? page.Elements["/Resources"] : null) is { } own) return own;
        var resources = new PdfDictionary(doc);
        for (var parent = Dict(page.Elements.ContainsKey("/Parent") ? page.Elements["/Parent"] : null); parent != null;
             parent = Dict(parent.Elements.ContainsKey("/Parent") ? parent.Elements["/Parent"] : null))
        {
            if (Dict(parent.Elements.ContainsKey("/Resources") ? parent.Elements["/Resources"] : null) is { } inherited)
            {
                foreach (var key in inherited.Elements.Keys) resources.Elements[key] = inherited.Elements[key];
                break;
            }
        }
        page.Elements["/Resources"] = resources;
        return resources;
    }

    /// <summary>리소스 사전 안의 하위 사전(/Font, /XObject, /ExtGState)을 돌려주거나 만든다. 간접 객체여도 처리한다.</summary>
    public static PdfDictionary ResourceCategory(PdfDocument doc, PdfDictionary resources, string category)
    {
        if (resources.Elements.ContainsKey(category) && Dict(resources.Elements[category]) is { } existing) return existing;
        var created = new PdfDictionary(doc);
        resources.Elements[category] = created;
        return created;
    }

    public static PdfReference Ref(PdfObject obj) =>
        obj.Reference ?? throw new InvalidOperationException("The object must be indirect (added to the document) first.");
}
