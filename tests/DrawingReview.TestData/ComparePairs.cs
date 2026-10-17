using DrawingReview.Engine.Geometry;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DrawingReview.TestData;

/// <summary>
/// 도면 비교 검수용 가상 도면 쌍 (Drawing Difference Checker 기획서 12장).
/// 같은 기본 도면에서 한 가지씩 바꾼 개정본을 만들고, 바뀐 위치를 함께 알려 준다.
/// </summary>
public static class ComparePairs
{
    public const float Width = 1191, Height = 842; // A3 가로

    /// <summary>개정 내용 선택.</summary>
    public sealed record Revision
    {
        public bool AddLine { get; init; }
        public bool RemoveBeam { get; init; }
        public double MoveBeamDx { get; init; }
        public string Dimension { get; init; } = "3000";
        public string Note { get; init; } = "VERIFY CLEARANCE AT BEAM";
        public string TitleDate { get; init; } = "2026-09-01";
        public bool AddFarDetails { get; init; }
        /// <summary>도면 전체를 조금 옮긴다 (렌더링 미세 차이 흉내).</summary>
        public double ShiftX { get; init; }
        public double ShiftY { get; init; }
    }

    // 바뀐 부분의 위치 (개정 도면 좌표, 기본 도면 기준)
    public static readonly PageRect AddedLineArea = new(820, 180, 1020, 190);
    public static readonly PageRect BeamB2Area = new(400, 380, 600, 390);
    public static readonly PageRect DimensionTextArea = new(470, 545, 540, 570);
    public static readonly PageRect NoteArea = new(700, 300, 980, 325);
    public static readonly PageRect TitleBlock = new(860, 700, 1160, 812);
    public static readonly PageRect[] FarDetailAreas =
    [
        new(120, 120, 180, 180), new(1040, 400, 1100, 460), new(150, 620, 230, 660),
    ];

    public static byte[] Sheet(Revision? r = null)
    {
        r ??= new Revision();
        using var doc = new PdfDocument();
        var page = PdfSketch.NewPage(doc, Width, Height);
        var s = new PdfSketch(doc, page);
        double dx = r.ShiftX, dy = r.ShiftY;
        double X(double x) => x + dx;
        double Y(double y) => y + dy;

        // 테두리와 그리드
        s.DrawRect(X(30), Y(30), X(Width - 30), Y(Height - 30));
        s.Finish([0, 0, 0], 2);
        for (int i = 0; i <= 8; i++) s.DrawLine(X(100 + i * 120), Y(80), X(100 + i * 120), Y(680));
        for (int j = 0; j <= 5; j++) s.DrawLine(X(80), Y(100 + j * 110), X(1080), Y(100 + j * 110));
        s.Finish([0.6, 0.6, 0.6], 0.5, dashes: "[6 3] 0");

        // 그리드 버블과 번호 (특징점이 생기도록 다양한 모양)
        for (int i = 0; i <= 8; i++) s.DrawCircle(X(100 + i * 120), Y(62), 12);
        for (int j = 0; j <= 5; j++) s.DrawCircle(X(62), Y(100 + j * 110), 12);
        s.Finish([0, 0, 0], 1);
        for (int i = 0; i <= 8; i++) s.InsertText(X(96 + i * 120), Y(66), ((char)('A' + i)).ToString(), 11);
        for (int j = 0; j <= 5; j++) s.InsertText(X(58), Y(104 + j * 110), (j + 1).ToString(), 11);

        // 벽 (두꺼운 선)
        s.DrawRect(X(100), Y(100), X(1060), Y(650));
        s.DrawLine(X(580), Y(100), X(580), Y(650));
        s.DrawLine(X(100), Y(430), X(580), Y(430));
        s.Finish([0, 0, 0], 4);

        // 보 B1, B2, B3
        s.DrawLine(X(120), Y(260), X(560), Y(260));
        if (!r.RemoveBeam) s.DrawLine(X(400 + r.MoveBeamDx), Y(385), X(600 + r.MoveBeamDx), Y(385));
        s.DrawLine(X(620), Y(520), X(1040), Y(520));
        s.Finish([0, 0, 0], 2);
        s.InsertText(X(130), Y(252), "B1 W12x26", 9);
        s.InsertText(X(630), Y(512), "B3 W10x22", 9);
        if (!r.RemoveBeam) s.InsertText(X(410 + r.MoveBeamDx), Y(377), "B2", 9);

        // 계단·문 같은 세부 (원호·사선)
        for (int k = 0; k < 8; k++) s.DrawLine(X(700 + k * 14), Y(560), X(700 + k * 14), Y(630));
        s.DrawRect(X(700), Y(560), X(812), Y(630));
        s.DrawCircle(X(300), Y(560), 40);
        s.DrawLine(X(260), Y(560), X(340), Y(560));
        s.Finish([0, 0, 0], 1);

        // 치수선과 치수 글자
        s.DrawLine(X(400), Y(580), X(600), Y(580));
        s.DrawLine(X(400), Y(572), X(400), Y(588));
        s.DrawLine(X(600), Y(572), X(600), Y(588));
        s.Finish([0, 0, 0], 0.7);
        s.InsertText(X(480), Y(566), r.Dimension, 12);

        // 메모 글자
        s.InsertText(X(705), Y(318), r.Note, 12);

        // 추가 선 (개정)
        if (r.AddLine) { s.DrawLine(X(830), Y(185), X(1010), Y(185)); s.Finish([0, 0, 0], 2); }

        // 서로 멀리 떨어진 작은 추가 세부 3곳 (여러 변경 분리 시험)
        if (r.AddFarDetails)
        {
            s.DrawRect(X(130), Y(130), X(170), Y(170));
            s.Finish([0, 0, 0], 1.5);
            s.DrawOval(X(1050), Y(410), X(1090), Y(450));
            s.Finish([0, 0, 0], 1.5);
            s.DrawLine(X(160), Y(640), X(220), Y(640));
            s.Finish([0, 0, 0], 1.5);
        }

        // 제목란
        s.DrawRect(X(860), Y(700), X(1160), Y(812));
        s.DrawLine(X(860), Y(756), X(1160), Y(756));
        s.Finish([0, 0, 0], 1.5);
        s.InsertText(X(870), Y(735), "PROJECT: SAMPLE OFFICE", 12);
        s.InsertText(X(870), Y(790), "S-101   DATE " + r.TitleDate, 12);
        s.Commit();
        return PdfTools.ToBytes(doc);
    }

    /// <summary>
    /// 다른 축척·회전·위치로 출력된 개정본을 흉내 낸다: 원본 페이지를 축소·회전해 새 페이지에 얹는다.
    /// 원본은 (offsetX, offsetY)에서 시작하는 scale배 크기의 틀 안에 비율을 유지한 채 들어가고(가운데 맞춤),
    /// rotateDegrees만큼 화면 기준 반시계 방향으로 돈다. 회전하면 돌아간 전체가 틀 안에 들어가도록 더 줄어든다.
    /// extraLine이 있으면 개정 도면 좌표로 선을 하나 더 그린다.
    /// </summary>
    public static byte[] Transformed(byte[] source, double scale, int rotateDegrees, double offsetX, double offsetY, PageRect? extraLine = null)
    {
        using var doc = new PdfDocument();
        var page = PdfSketch.NewPage(doc, Width, Height);
        using var gfx = XGraphics.FromPdfPage(page);
        using var form = XPdfForm.FromStream(new MemoryStream(source));
        double w = form.PointWidth, h = form.PointHeight;
        double frameW = Width * scale, frameH = Height * scale;
        var rad = rotateDegrees * Math.PI / 180;
        double c = Math.Abs(Math.Cos(rad)), sn = Math.Abs(Math.Sin(rad));
        var fit = Math.Min(frameW / (w * c + h * sn), frameH / (w * sn + h * c));

        var state = gfx.Save();
        gfx.TranslateTransform(offsetX + frameW / 2, offsetY + frameH / 2);
        gfx.RotateTransform(-rotateDegrees); // 양수 = 화면 기준 반시계 방향 (비교 엔진이 재는 각도와 같은 부호)
        gfx.ScaleTransform(fit);
        gfx.DrawImage(form, -w / 2, -h / 2, w, h);
        gfx.Restore(state);

        if (extraLine is { } l)
            gfx.DrawLine(new XPen(XColors.Black, 2), l.X0, l.Y0, l.X1, l.Y1);
        gfx.Dispose();
        return PdfTools.ToBytes(doc);
    }
}
