using System.Globalization;
using System.Text;
using DrawingReview.Engine.Pdf;
using PdfSharp.Pdf;

namespace DrawingReview.TestData;

/// <summary>
/// 페이지 본문에 한글을 넣는 도우미. 글꼴 처리는 엔진의 <see cref="KoreanCidFont"/>를 함께 쓴다.
/// </summary>
public static class KoreanText
{
    /// <param name="x">회전 전 페이지 기준 왼쪽에서의 거리(pt).</param>
    /// <param name="baselineY">회전 전 페이지 기준 위에서 글자 기준선까지 거리(pt).</param>
    public static void Insert(PdfDocument doc, int pageIndex, double x, double baselineY, string text, double fontSize)
    {
        var page = doc.Pages[pageIndex];
        KoreanCidFont.AddToPageResources(doc, page, KoreanCidFont.GetOrCreate(doc), "KR1");
        var mediaHeight = page.Height.Point;
        var content = string.Format(CultureInfo.InvariantCulture,
            "q BT /KR1 {0:0.###} Tf 0 g 1 0 0 1 {1:0.###} {2:0.###} Tm <{3}> Tj ET Q\n", fontSize, x, mediaHeight - baselineY, KoreanCidFont.Hex(text));
        page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(content));
    }
}
