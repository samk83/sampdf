using System.Text;
using DrawingReview.Engine.MuPdf;
using MuPDF.NET;

namespace DrawingReview.TestData;

/// <summary>
/// 페이지 본문에 한글을 넣는 도우미. 글꼴 처리는 엔진의 <see cref="KoreanCidFont"/>를 함께 쓴다.
/// </summary>
public static class KoreanText
{
    /// <param name="x">회전 전 페이지 기준 왼쪽에서의 거리(pt).</param>
    /// <param name="baselineY">회전 전 페이지 기준 위에서 글자 기준선까지 거리(pt).</param>
    public static void Insert(Document doc, int pageIndex, float x, float baselineY, string text, float fontSize)
    {
        var page = doc[pageIndex];
        int font = KoreanCidFont.GetOrCreate(doc);
        KoreanCidFont.AddToPageResources(doc, page.Xref, font, "KR1");

        var mediaHeight = page.MediaBox.Height;
        var content = FormattableString.Invariant(
            $"q BT /KR1 {fontSize} Tf 1 0 0 1 {x} {mediaHeight - baselineY} Tm <{KoreanCidFont.Hex(text)}> Tj ET Q");

        int stream = doc.GetNewXref();
        doc.UpdateObject(stream, "<<>>");
        doc.UpdateStream(stream, Encoding.ASCII.GetBytes(content));

        var (kind, value) = doc.XrefGetKey(page.Xref, "Contents");
        var newContents = kind == "array"
            ? value.TrimEnd().TrimEnd(']') + $" {stream} 0 R]"
            : $"[{value} {stream} 0 R]";
        doc.XrefSetKey(page.Xref, "Contents", newContents);
    }
}
