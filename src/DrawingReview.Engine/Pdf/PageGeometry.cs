using DrawingReview.Engine.Geometry;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// "보이는 페이지 좌표"(왼쪽 위 원점, 회전·CropBox 반영, pt) ↔ "PDF 좌표"(파일 안의 좌표, 왼쪽 아래 원점) 변환.
///
/// PDF 파일의 주석·글자 위치는 모두 PDF 좌표로 적혀 있고, 화면과 UI는 보이는 좌표만 쓴다.
/// Box는 실제로 보이는 영역(CropBox ∩ MediaBox, PDF 좌표)이고 Rotation은 /Rotate(0·90·180·270)다.
/// </summary>
public sealed record PageGeometry(double BoxX0, double BoxY0, double BoxX1, double BoxY1, int Rotation)
{
    /// <summary>회전 전 너비·높이.</summary>
    public double BoxWidth => BoxX1 - BoxX0;
    public double BoxHeight => BoxY1 - BoxY0;

    /// <summary>보이는 너비·높이 (90°·270°면 가로세로가 바뀐다).</summary>
    public double Width => Rotation is 90 or 270 ? BoxHeight : BoxWidth;
    public double Height => Rotation is 90 or 270 ? BoxWidth : BoxHeight;

    public PagePoint ToVisible(double pdfX, double pdfY)
    {
        // 1) 회전 전 페이지 좌표 (왼쪽 위 원점)  2) /Rotate 만큼 시계 방향으로 돌린 화면 좌표
        double ux = pdfX - BoxX0, uy = BoxY1 - pdfY;
        return Rotation switch
        {
            90 => new PagePoint(BoxHeight - uy, ux),
            180 => new PagePoint(BoxWidth - ux, BoxHeight - uy),
            270 => new PagePoint(uy, BoxWidth - ux),
            _ => new PagePoint(ux, uy),
        };
    }

    public (double X, double Y) ToPdf(PagePoint p)
    {
        var (ux, uy) = Rotation switch
        {
            90 => (p.Y, BoxHeight - p.X),
            180 => (BoxWidth - p.X, BoxHeight - p.Y),
            270 => (BoxWidth - p.Y, p.X),
            _ => (p.X, p.Y),
        };
        return (BoxX0 + ux, BoxY1 - uy);
    }

    /// <summary>PDF 좌표의 사각형(모서리 순서 무관) → 보이는 좌표.</summary>
    public PageRect ToVisibleRect(double x0, double y0, double x1, double y1)
    {
        var a = ToVisible(x0, y0);
        var b = ToVisible(x1, y1);
        return new PageRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    /// <summary>보이는 좌표의 사각형 → PDF 좌표 (x0 ≤ x1, y0 ≤ y1 로 정리).</summary>
    public (double X0, double Y0, double X1, double Y1) ToPdfRect(PageRect r)
    {
        var a = ToPdf(new PagePoint(r.X0, r.Y0));
        var b = ToPdf(new PagePoint(r.X1, r.Y1));
        return (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    /// <summary>
    /// 보이는 좌표의 사각형 안에 "화면에서 똑바로 선" 좌표계를 만든다: 원점은 화면 기준 왼쪽 아래 모서리,
    /// x는 화면 오른쪽, y는 화면 위쪽. 글상자 글자를 회전 페이지에서도 바로 보이게 쓸 때 쓴다 (cm 연산자 값).
    /// </summary>
    public (double A, double B, double C, double D, double E, double F) UprightFrame(PageRect r)
    {
        var origin = ToPdf(new PagePoint(r.X0, r.Y1));
        var xAxis = ToPdf(new PagePoint(r.X0 + 1, r.Y1));
        var yAxis = ToPdf(new PagePoint(r.X0, r.Y1 - 1));
        return (xAxis.X - origin.X, xAxis.Y - origin.Y, yAxis.X - origin.X, yAxis.Y - origin.Y, origin.X, origin.Y);
    }
}
