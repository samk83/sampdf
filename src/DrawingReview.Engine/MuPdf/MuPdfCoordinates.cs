using DrawingReview.Engine.Geometry;
using MuPDF.NET;

namespace DrawingReview.Engine.MuPdf;

/// <summary>
/// 보이는 페이지 좌표(PagePoint/PageRect) ↔ MuPDF 좌표 변환.
///
/// 0단계 검증 결과(docs/engine-validation.md):
/// - Square/Circle/Line/Polygon 생성 함수는 보이는 좌표를 받아 내부에서 회전을 처리한다.
/// - Ink/Highlight/FreeText 생성 함수는 회전 전(unrotated) 좌표를 받으므로 DerotationMatrix로 직접 바꿔야 한다.
/// - Annot.Rect는 항상 회전 전 좌표로 돌아오므로 RotationMatrix를 곱해 보이는 좌표로 바꾼다.
/// </summary>
internal static class MuPdfCoordinates
{
    public static Point ToMu(PagePoint p) => new((float)p.X, (float)p.Y);

    public static Rect ToMu(PageRect r) => new((float)r.X0, (float)r.Y0, (float)r.X1, (float)r.Y1);

    public static Point ToUnrotated(Page page, PagePoint p) => ToMu(p) * page.DerotationMatrix;

    public static Rect ToUnrotated(Page page, PageRect r) => Normalize(ToMu(r) * page.DerotationMatrix);

    public static PageRect FromUnrotated(Page page, Rect r) => FromMu(Normalize(r * page.RotationMatrix));

    public static PageRect FromMu(Rect r) => new(r.X0, r.Y0, r.X1, r.Y1);

    private static Rect Normalize(Rect r) =>
        new(Math.Min(r.X0, r.X1), Math.Min(r.Y0, r.Y1), Math.Max(r.X0, r.X1), Math.Max(r.Y0, r.Y1));
}
