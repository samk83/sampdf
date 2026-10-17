using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 증분 저장(incremental update): 원본 바이트 뒤에 바뀐 객체와 새 교차 참조 표(xref)·트레일러만 덧붙인다 (PDF 규격 7.5.6).
/// 원본 바이트는 그대로 남으므로 빠르고, 전자서명 같은 원본 구조도 건드리지 않는다.
/// 원본이 xref 스트림(PDF 1.5)을 쓰면 같은 방식(xref 스트림)으로 덧붙인다.
/// </summary>
internal static partial class IncrementalWriter
{
    /// <summary>
    /// 원본이 xref 스트림을 쓰면 PDFsharp는 그 스트림 객체 번호를 표에서 빼 버려서, 새 객체가 같은 번호를 받을 수 있다.
    /// 그러면 다시 열 때 PDFsharp가 새 객체도 xref 스트림으로 보고 버린다. 번호가 /Size 이상부터 나오도록 빈 자리를 채워 둔다.
    /// </summary>
    public static void ReserveObjectNumbers(PdfDocument doc, byte[] original)
    {
        var (_, usesXrefStream, size, _) = ReadTrailer(original);
        if (!usesXrefStream) return;
        PdfDictionary placeholder;
        do
        {
            placeholder = new PdfDictionary(doc); // 참조하지도, 파일에 쓰지도 않는 빈 자리
            doc.Internals.AddObject(placeholder);
        } while (PdfInternals.GetObjectNumber(placeholder) < size - 1);
    }

    public static byte[] Append(byte[] original, PdfDocument doc, IReadOnlyCollection<PdfObject> changed)
    {
        if (changed.Count == 0) return original;
        var (prevOffset, usesXrefStream, originalSize, info) = ReadTrailer(original);

        using var ms = new MemoryStream(original.Length + 4096);
        ms.Write(original);
        if (original.Length == 0 || original[^1] is not ((byte)'\n' or (byte)'\r')) ms.WriteByte((byte)'\n');

        var entries = new SortedDictionary<int, (long Offset, int Generation)>();
        foreach (var obj in changed.OrderBy(PdfInternals.GetObjectNumber))
        {
            var number = PdfInternals.GetObjectNumber(obj);
            entries[number] = (ms.Position, PdfInternals.GenerationNumber(obj));
            doc.Internals.WriteObject(ms, obj);
            if (ms.Position > 0) ms.WriteByte((byte)'\n');
        }

        var catalog = PdfInternals.GetObjectNumber(doc.Internals.Catalog);
        // /Info는 원본 트레일러 값을 그대로 잇는다. doc.Info를 읽으면 PDFsharp가 없는 Info를 새로 만들어 버려(파일에 없는 객체) 쓰지 않는다.
        var size = Math.Max(originalSize, entries.Keys.Max() + 1);
        var xrefOffset = ms.Position;

        if (usesXrefStream)
        {
            var xrefNumber = size; // 새 번호 하나를 xref 스트림 자신에게 쓴다
            size = xrefNumber + 1;
            entries[xrefNumber] = (xrefOffset, 0);
            WriteXrefStream(ms, doc, entries, xrefNumber, size, catalog, info, prevOffset);
        }
        else
        {
            WriteXrefTable(ms, doc, entries, size, catalog, info, prevOffset);
        }

        ms.Write(Encoding.ASCII.GetBytes($"startxref\n{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n"));
        return ms.ToArray();
    }

    private static void WriteXrefTable(MemoryStream ms, PdfDocument doc, SortedDictionary<int, (long Offset, int Generation)> entries,
        int size, int catalog, int info, long prev)
    {
        var sb = new StringBuilder("xref\n");
        foreach (var (start, group) in Runs(entries))
        {
            sb.Append(Inv($"{start} {group.Count}\n"));
            foreach (var (offset, generation) in group)
                sb.Append(Inv($"{offset:0000000000} {generation:00000} n \n"));
        }
        sb.Append("trailer\n").Append(Inv($"<< /Size {size} /Root {catalog} 0 R"));
        if (info > 0) sb.Append(Inv($" /Info {info} 0 R"));
        sb.Append(Inv($" /Prev {prev}"));
        var ids = DocumentIds(doc);
        if (ids != null) sb.Append($" /ID [<{ids.Value.First}> <{ids.Value.Second}>]");
        sb.Append(" >>\n");
        ms.Write(Encoding.ASCII.GetBytes(sb.ToString()));
    }

    private static void WriteXrefStream(MemoryStream ms, PdfDocument doc, SortedDictionary<int, (long Offset, int Generation)> entries,
        int number, int size, int catalog, int info, long prev)
    {
        // /W [1 4 2]: 종류 1바이트, 오프셋 4바이트, 세대 2바이트
        var data = new MemoryStream();
        var index = new StringBuilder();
        foreach (var (start, group) in Runs(entries))
        {
            index.Append(Inv($"{start} {group.Count} "));
            foreach (var (offset, generation) in group)
            {
                data.WriteByte(1);
                data.WriteByte((byte)(offset >> 24)); data.WriteByte((byte)(offset >> 16)); data.WriteByte((byte)(offset >> 8)); data.WriteByte((byte)offset);
                data.WriteByte((byte)(generation >> 8)); data.WriteByte((byte)generation);
            }
        }
        var sb = new StringBuilder();
        sb.Append(Inv($"{number} 0 obj\n<< /Type /XRef /Size {size} /W [1 4 2] /Index [{index.ToString().TrimEnd()}] /Root {catalog} 0 R"));
        if (info > 0) sb.Append(Inv($" /Info {info} 0 R"));
        sb.Append(Inv($" /Prev {prev}"));
        var ids = DocumentIds(doc);
        if (ids != null) sb.Append($" /ID [<{ids.Value.First}> <{ids.Value.Second}>]");
        sb.Append(Inv($" /Length {data.Length} >>\nstream\n"));
        ms.Write(Encoding.ASCII.GetBytes(sb.ToString()));
        data.Position = 0;
        data.CopyTo(ms);
        ms.Write(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));
    }

    /// <summary>연속한 객체 번호 묶음 (xref 하위 구역 하나씩).</summary>
    private static IEnumerable<(int Start, List<(long Offset, int Generation)> Group)> Runs(SortedDictionary<int, (long Offset, int Generation)> entries)
    {
        int? start = null, last = null;
        var group = new List<(long, int)>();
        foreach (var (number, entry) in entries)
        {
            if (start != null && number != last + 1)
            {
                yield return (start.Value, group);
                group = [];
                start = null;
            }
            start ??= number;
            last = number;
            group.Add(entry);
        }
        if (start != null) yield return (start.Value, group);
    }

    private static (string First, string Second)? DocumentIds(PdfDocument doc)
    {
        var first = doc.Internals.FirstDocumentID;
        var second = doc.Internals.SecondDocumentID;
        if (string.IsNullOrEmpty(first)) return null;
        if (string.IsNullOrEmpty(second)) second = first;
        return (Hex(first), Hex(second));
    }

    /// <summary>PDFsharp는 /ID 바이트를 글자 하나 = 바이트 하나인 문자열로 들고 있다.</summary>
    private static string Hex(string raw) => string.Concat(raw.Select(ch => ((int)ch & 0xFF).ToString("X2")));

    /// <summary>원본의 마지막 startxref 위치, xref 종류(표/스트림), 트레일러의 /Size와 /Info 객체 번호(없으면 0).</summary>
    private static (long PrevOffset, bool UsesXrefStream, int Size, int Info) ReadTrailer(byte[] pdf)
    {
        var tailStart = Math.Max(0, pdf.Length - 2048);
        var tail = Encoding.Latin1.GetString(pdf, tailStart, pdf.Length - tailStart);
        var idx = tail.LastIndexOf("startxref", StringComparison.Ordinal);
        if (idx < 0)
        {
            var all = Encoding.Latin1.GetString(pdf);
            idx = all.LastIndexOf("startxref", StringComparison.Ordinal);
            if (idx < 0) throw new InvalidDataException("The PDF has no startxref; it cannot be updated incrementally.");
            tail = all;
        }
        var m = StartXref().Match(tail, idx);
        if (!m.Success) throw new InvalidDataException("The PDF startxref value could not be read.");
        var offset = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        if (offset <= 0 || offset >= pdf.Length) throw new InvalidDataException("The PDF startxref offset is out of range.");

        // xref 표면 "xref" 키워드로 시작하고, 아니면 "n g obj << /Type /XRef … >>" 스트림이다
        var head = Encoding.Latin1.GetString(pdf, (int)offset, Math.Min(4096, pdf.Length - (int)offset));
        var usesStream = !head.TrimStart().StartsWith("xref", StringComparison.Ordinal);
        string region;
        if (usesStream) region = head;
        else
        {
            // 표 뒤의 "trailer" 사전: 표가 아무리 커도 바이트로 찾아 그 부분만 읽는다
            var t = pdf.AsSpan((int)offset).IndexOf("trailer"u8);
            if (t < 0) throw new InvalidDataException("The PDF xref table has no trailer.");
            var start = (int)offset + t;
            region = Encoding.Latin1.GetString(pdf, start, Math.Min(4096, pdf.Length - start));
        }
        var size = SizeKey().Match(region) is { Success: true } sm ? int.Parse(sm.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var info = InfoKey().Match(region) is { Success: true } im ? int.Parse(im.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        return (offset, usesStream, size, info);
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"startxref\s+(\d+)")]
    private static partial Regex StartXref();

    [GeneratedRegex(@"/Size\s+(\d+)")]
    private static partial Regex SizeKey();

    [GeneratedRegex(@"/Info\s+(\d+)\s+\d+\s+R")]
    private static partial Regex InfoKey();
}
