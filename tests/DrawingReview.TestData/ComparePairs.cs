using DrawingReview.Engine.Geometry;
using MuPDF.NET;

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
        using var doc = new Document();
        doc.NewPage(width: Width, height: Height);
        var page = doc[0];
        var s = page.NewShape();
        float dx = (float)r.ShiftX, dy = (float)r.ShiftY;
        Point P(double x, double y) => new((float)x + dx, (float)y + dy);

        // 테두리와 그리드
        s.DrawRect(new Rect(P(30, 30), P(Width - 30, Height - 30)));
        s.Finish(color: [0, 0, 0], width: 2, closePath: true);
        for (int i = 0; i <= 8; i++) s.DrawLine(P(100 + i * 120, 80), P(100 + i * 120, 680));
        for (int j = 0; j <= 5; j++) s.DrawLine(P(80, 100 + j * 110), P(1080, 100 + j * 110));
        s.Finish(color: [0.6f, 0.6f, 0.6f], width: 0.5f, dashes: "[6 3] 0");

        // 그리드 버블과 번호 (특징점이 생기도록 다양한 모양)
        for (int i = 0; i <= 8; i++) s.DrawCircle(P(100 + i * 120, 62), 12);
        for (int j = 0; j <= 5; j++) s.DrawCircle(P(62, 100 + j * 110), 12);
        s.Finish(color: [0, 0, 0], width: 1);
        for (int i = 0; i <= 8; i++) s.InsertText(P(96 + i * 120, 66), ((char)('A' + i)).ToString(), fontSize: 11);
        for (int j = 0; j <= 5; j++) s.InsertText(P(58, 104 + j * 110), (j + 1).ToString(), fontSize: 11);

        // 벽 (두꺼운 선)
        s.DrawRect(new Rect(P(100, 100), P(1060, 650)));
        s.DrawLine(P(580, 100), P(580, 650));
        s.DrawLine(P(100, 430), P(580, 430));
        s.Finish(color: [0, 0, 0], width: 4, closePath: true);

        // 보 B1, B2, B3
        s.DrawLine(P(120, 260), P(560, 260));
        if (!r.RemoveBeam) s.DrawLine(P(400 + r.MoveBeamDx, 385), P(600 + r.MoveBeamDx, 385));
        s.DrawLine(P(620, 520), P(1040, 520));
        s.Finish(color: [0, 0, 0], width: 2);
        s.InsertText(P(130, 252), "B1 W12x26", fontSize: 9);
        s.InsertText(P(630, 512), "B3 W10x22", fontSize: 9);
        if (!r.RemoveBeam) s.InsertText(P(410 + r.MoveBeamDx, 377), "B2", fontSize: 9);

        // 계단·문 같은 세부 (원호·사선)
        for (int k = 0; k < 8; k++) s.DrawLine(P(700 + k * 14, 560), P(700 + k * 14, 630));
        s.DrawRect(new Rect(P(700, 560), P(812, 630)));
        s.DrawCircle(P(300, 560), 40);
        s.DrawLine(P(260, 560), P(340, 560));
        s.Finish(color: [0, 0, 0], width: 1, closePath: false);

        // 치수선과 치수 글자
        s.DrawLine(P(400, 580), P(600, 580));
        s.DrawLine(P(400, 572), P(400, 588));
        s.DrawLine(P(600, 572), P(600, 588));
        s.Finish(color: [0, 0, 0], width: 0.7f);
        s.InsertText(P(480, 566), r.Dimension, fontSize: 12);

        // 메모 글자
        s.InsertText(P(705, 318), r.Note, fontSize: 12);

        // 추가 선 (개정)
        if (r.AddLine) { s.DrawLine(P(830, 185), P(1010, 185)); s.Finish(color: [0, 0, 0], width: 2); }

        // 서로 멀리 떨어진 작은 추가 세부 3곳 (여러 변경 분리 시험)
        if (r.AddFarDetails)
        {
            // MuPDF.NET Shape.DrawCircle은 앞에 Finish한 도형이 있으면 원을 그리지 않는다(0단계 이후 발견). DrawOval을 쓴다.
            s.DrawRect(new Rect(P(130, 130), P(170, 170)));
            s.Finish(color: [0, 0, 0], width: 1.5f, closePath: true);
            s.DrawOval(new Rect(P(1050, 410), P(1090, 450)));
            s.Finish(color: [0, 0, 0], width: 1.5f);
            s.DrawLine(P(160, 640), P(220, 640));
            s.Finish(color: [0, 0, 0], width: 1.5f);
        }

        // 제목란
        s.DrawRect(new Rect(P(860, 700), P(1160, 812)));
        s.DrawLine(P(860, 756), P(1160, 756));
        s.Finish(color: [0, 0, 0], width: 1.5f, closePath: true);
        s.InsertText(P(870, 735), "PROJECT: SAMPLE OFFICE", fontSize: 12);
        s.InsertText(P(870, 790), "S-101   DATE " + r.TitleDate, fontSize: 12);
        s.Commit();
        return doc.Write(garbage: true, deflate: true);
    }

    /// <summary>
    /// 다른 축척·회전·위치로 출력된 개정본을 흉내 낸다: 원본 페이지를 축소·회전해 새 페이지에 얹는다.
    /// extraLine이 있으면 개정 도면 좌표로 선을 하나 더 그린다.
    /// </summary>
    public static byte[] Transformed(byte[] source, double scale, int rotateDegrees, double offsetX, double offsetY, PageRect? extraLine = null)
    {
        using var src = new Document(stream: source, fileType: "pdf");
        using var doc = new Document();
        doc.NewPage(width: Width, height: Height);
        var page = doc[0];
        var w = Width * scale;
        var h = Height * scale;
        var rect = new Rect((float)offsetX, (float)offsetY, (float)(offsetX + w), (float)(offsetY + h));
        page.ShowPdfPage(rect, src, 0, keepProportion: true, rotate: rotateDegrees);
        if (extraLine is { } l)
        {
            page = doc[0];
            var s = page.NewShape();
            s.DrawLine(new Point((float)l.X0, (float)l.Y0), new Point((float)l.X1, (float)l.Y1));
            s.Finish(color: [0, 0, 0], width: 2);
            s.Commit();
        }
        return doc.Write(garbage: true, deflate: true);
    }
}
