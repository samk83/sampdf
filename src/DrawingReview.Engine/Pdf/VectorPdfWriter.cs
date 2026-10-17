using System.Text;
using DrawingReview.Engine.Persistence;
using DrawingReview.Engine.Pdfium;
using DrawingReview.Engine.Vector;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 벡터 페이지를 새 PDF로 쓴다. 선이 수만 개여도 빠르도록 페이지 내용(content stream)을 직접 만든다.
/// 글자: 영문·숫자는 Helvetica(내장 기본 글꼴), 그 밖의 글자(한글 등)는 Adobe-Korea1 CID 참조.
/// </summary>
internal static class VectorPdfWriter
{
    public static void Write(string path, IReadOnlyList<VectorPage> pages)
    {
        if (pages.Count == 0) throw new ArgumentException("At least one page is required.", nameof(pages));
        using var doc = new PdfDocument();
        foreach (var vp in pages)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(vp.Width);
            page.Height = XUnit.FromPoint(vp.Height);
            var content = new StringBuilder("q 1 J 1 j\n");
            bool usesLatin = false, usesCid = false;
            string? lastStroke = null;
            foreach (var item in vp.Items)
            {
                switch (item)
                {
                    case VectorPolyline pl when pl.Points.Count >= 2:
                        {
                            var stroke = Invariant($"{pl.Color.R} {pl.Color.G} {pl.Color.B} RG {pl.LineWidth} w");
                            if (stroke != lastStroke) { content.Append(stroke).Append('\n'); lastStroke = stroke; }
                            for (int i = 0; i < pl.Points.Count; i++)
                                content.Append(Invariant($"{pl.Points[i].X:0.###} {vp.Height - pl.Points[i].Y:0.###} ")).Append(i == 0 ? "m " : "l ");
                            content.Append(pl.Closed ? "h S\n" : "S\n");
                            break;
                        }
                    case VectorText t when !string.IsNullOrWhiteSpace(t.Text) && t.Size > 0:
                        {
                            var a = t.RotationDegrees * Math.PI / 180;
                            var (c, s) = (Math.Cos(a), Math.Sin(a));
                            var latin = t.Text.All(ch => ch >= 32 && ch < 127);
                            usesLatin |= latin;
                            usesCid |= !latin;
                            var font = latin ? "/F1" : "/KR1";
                            var str = latin ? "(" + EscapeLiteral(t.Text) + ")" : "<" + KoreanCidFont.Hex(t.Text) + ">";
                            content.Append(Invariant($"BT {font} {t.Size:0.###} Tf {t.Color.R} {t.Color.G} {t.Color.B} rg {c:0.#####} {s:0.#####} {-s:0.#####} {c:0.#####} {t.Baseline.X:0.###} {vp.Height - t.Baseline.Y:0.###} Tm "))
                                .Append(str).Append(" Tj ET\n");
                            break;
                        }
                }
            }
            content.Append("Q\n");

            var stream = page.Contents.AppendContent();
            stream.CreateStream(Ascii(content.ToString()));
            if (content.Length > 256) stream.Stream.Zip();

            if (usesLatin) KoreanCidFont.AddToPageResources(doc, page, StandardFonts.GetOrCreateHelvetica(doc), "F1");
            if (usesCid) KoreanCidFont.AddToPageResources(doc, page, KoreanCidFont.GetOrCreate(doc), "KR1");
        }
        var expected = pages.Count;
        SafeFileSaver.Save(path, temp =>
        {
            using var fs = File.Create(temp);
            doc.Save(fs, closeStream: false);
        }, temp =>
        {
            using var check = PdfiumView.Open(File.ReadAllBytes(temp), null, out _);
            if (check == null || check.PageCount != expected) throw new IOException("The written PDF could not be verified.");
        });
    }
}
