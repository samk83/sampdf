using MuPDF.NET;

namespace DrawingReview.TestData;

/// <summary>
/// 실제 도면 대신 쓰는 가상 도면 PDF 생성기.
/// CAD/Revit 출력에서 자주 만나는 조건(대형 용지, 회전 페이지, CropBox, 한글, 기존 주석·링크, 많은 벡터)을 흉내 낸다.
/// </summary>
public static class VirtualDrawings
{
    // 용지 크기 (pt, 1in = 72pt)
    public const float ArchDWidth = 36 * 72, ArchDHeight = 24 * 72;   // 2592 x 1728
    public const float A3Width = 1191, A3Height = 842;
    public const float A4Width = 595, A4Height = 842;

    /// <summary>plan set의 페이지 구성. 테스트에서 페이지 의미를 이름으로 찾기 위해 쓴다.</summary>
    public static class PlanSetPages
    {
        public const int FloorPlan = 0;        // ARCH D 가로, 회전 없음, 기존 메모 주석·URL 링크 포함
        public const int Rotated90 = 1;        // 세로 용지 + /Rotate 90 (CAD 출력에서 흔함)
        public const int CroppedA3 = 2;        // MediaBox보다 작은 CropBox
        public const int KoreanSpec = 3;       // A4, 한글·영문 혼합 본문, 1페이지로 가는 내부 링크
        public const int Rotated180 = 4;
        public const int Rotated270 = 5;
        public const int Count = 6;
    }

    /// <summary>기존(다른 프로그램이 만든 것으로 가정한) 주석·링크 개수. 저장 후 보존 여부 확인용.</summary>
    public const int PreexistingAnnotationCount = 1;

    public static byte[] CreatePlanSet()
    {
        using var doc = new Document();

        doc.NewPage(width: ArchDWidth, height: ArchDHeight);
        DrawFloorPlan(doc[PlanSetPages.FloorPlan], ArchDWidth, ArchDHeight, "A-101", "FIRST FLOOR PLAN / 1층 평면도");

        doc.NewPage(width: ArchDHeight, height: ArchDWidth);
        DrawFloorPlan(doc[PlanSetPages.Rotated90], ArchDHeight, ArchDWidth, "A-102", "ROOF FRAMING PLAN / 지붕 구조 평면도");
        doc[PlanSetPages.Rotated90].SetRotation(90);

        doc.NewPage(width: A3Width, height: A3Height);
        DrawFloorPlan(doc[PlanSetPages.CroppedA3], A3Width, A3Height, "S-301", "SECTION DETAIL / 단면 상세");
        doc[PlanSetPages.CroppedA3].SetCropBox(new Rect(60, 40, A3Width - 30, A3Height - 50));

        doc.NewPage(width: A4Width, height: A4Height);
        DrawSpecSheet(doc[PlanSetPages.KoreanSpec]);

        doc.NewPage(width: A3Width, height: A3Height);
        DrawFloorPlan(doc[PlanSetPages.Rotated180], A3Width, A3Height, "M-201", "MECHANICAL PLAN / 기계 평면도");
        doc[PlanSetPages.Rotated180].SetRotation(180);

        doc.NewPage(width: A3Height, height: A3Width);
        DrawFloorPlan(doc[PlanSetPages.Rotated270], A3Height, A3Width, "E-401", "ELECTRICAL PLAN / 전기 평면도");
        doc[PlanSetPages.Rotated270].SetRotation(270);

        AddPreexistingItems(doc);
        return doc.Write(garbage: true, deflate: true);
    }

    /// <summary>성능 측정용: 선이 매우 많은 대형 도면 1장.</summary>
    public static byte[] CreateDenseVectorSheet(int gridDivisions = 160)
    {
        using var doc = new Document();
        const float w = 42 * 72, h = 30 * 72; // ARCH E1 가로
        doc.NewPage(width: w, height: h);
        var page = doc[0];
        var shape = page.NewShape();
        var rnd = new Random(42);
        // 격자 + 셀마다 대각선 해치: (n+1)*2 + n*n*2 개 선
        for (int i = 0; i <= gridDivisions; i++)
        {
            float x = 50 + i * (w - 100) / gridDivisions, y = 50 + i * (h - 100) / gridDivisions;
            shape.DrawLine(new Point(x, 50), new Point(x, h - 50));
            shape.DrawLine(new Point(50, y), new Point(w - 50, y));
        }
        shape.Finish(color: [0, 0, 0], width: 0.3f);
        float cw = (w - 100) / gridDivisions, ch = (h - 100) / gridDivisions;
        for (int i = 0; i < gridDivisions; i++)
            for (int j = 0; j < gridDivisions; j++)
            {
                float x = 50 + i * cw, y = 50 + j * ch;
                shape.DrawLine(new Point(x, y), new Point(x + cw, y + ch));
                if (rnd.Next(3) == 0) shape.DrawLine(new Point(x + cw, y), new Point(x, y + ch));
            }
        shape.Finish(color: [0.2f, 0.2f, 0.6f], width: 0.15f);
        shape.InsertText(new Point(80, h - 70), "DENSE VECTOR TEST SHEET", fontSize: 36);
        shape.Commit();
        return doc.Write(garbage: true, deflate: true);
    }

    private static void DrawFloorPlan(Page page, float w, float h, string sheet, string title)
    {
        var s = page.NewShape();
        float m = Math.Min(w, h) * 0.03f;

        // 테두리
        s.DrawRect(new Rect(m, m, w - m, h - m));
        s.Finish(color: [0, 0, 0], width: 2, closePath: true);

        // 그리드 (A~F, 1~8) — 점선
        float left = m * 3, top = m * 3, right = w - m * 3, bottom = h - m * 7;
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.DrawLine(new Point(x, top), new Point(x, bottom));
        }
        for (int j = 0; j < 6; j++)
        {
            float y = top + j * (bottom - top) / 5;
            s.DrawLine(new Point(left, y), new Point(right, y));
        }
        s.Finish(color: [0.5f, 0.5f, 0.5f], width: 0.5f, dashes: "[6 3] 0");

        // 그리드 버블
        float r = m * 0.6f;
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.DrawCircle(new Point(x, top - r * 1.5f), r);
        }
        s.Finish(color: [0, 0, 0], width: 1);
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.InsertText(new Point(x - r * 0.35f, top - r * 1.2f), (i + 1).ToString(), fontSize: r);
        }

        // 벽체 (두꺼운 선)
        s.DrawRect(new Rect(left + 20, top + 20, right - 20, bottom - 20));
        s.DrawLine(new Point((left + right) / 2, top + 20), new Point((left + right) / 2, bottom - 20));
        s.DrawLine(new Point(left + 20, (top + bottom) / 2), new Point((left + right) / 2, (top + bottom) / 2));
        s.Finish(color: [0, 0, 0], width: 4, closePath: true);

        // 트러스·보 표기 (검색 테스트용 영문 키워드)
        s.InsertText(new Point(left + 40, top + 60), "NEW TRUSS T-1 @ 600 O.C.", fontSize: m * 0.5f);
        s.InsertText(new Point(left + 40, top + 60 + m), "EXISTING BEAM W12x26", fontSize: m * 0.5f);

        // 제목란 (오른쪽 아래)
        var tb = new Rect(w - m - Math.Min(w * 0.3f, 900), h - m * 6, w - m, h - m);
        s.DrawRect(tb);
        s.DrawLine(new Point(tb.X0, tb.Y0 + tb.Height / 2), new Point(tb.X1, tb.Y0 + tb.Height / 2));
        s.Finish(color: [0, 0, 0], width: 1.5f, closePath: true);
        s.InsertText(new Point(tb.X0 + 10, tb.Y0 + tb.Height * 0.35f), "PROJECT: SAMPLE OFFICE BUILDING", fontSize: m * 0.45f);
        s.InsertText(new Point(tb.X0 + 10, tb.Y0 + tb.Height * 0.85f), $"SHEET {sheet}", fontSize: m * 0.6f);
        s.Commit();

        // 한글 제목은 CJK 글꼴(뷰어 제공, 비포함)로 넣는다: 파일 크기를 작게 유지한다.
        page.InsertText(new Point(tb.X0 + tb.Width * 0.45f, tb.Y0 + tb.Height * 0.85f), title,
            fontSize: m * 0.4f, fontName: "korea");
    }

    private static void DrawSpecSheet(Page page)
    {
        page.InsertText(new Point(50, 70), "GENERAL NOTES / 일반 사항", fontSize: 18, fontName: "korea");
        string[] lines =
        [
            "1. 모든 치수는 현장에서 확인한다. VERIFY ALL DIMENSIONS IN FIELD.",
            "2. 신설 트러스와 기존 보 사이의 간격을 확인한다.",
            "   VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM.",
            "3. 구조 도면과 기계 도면의 간섭 여부를 검토한다.",
            "4. See sheet A-101 for floor plan. 평면도는 A-101 참조.",
        ];
        for (int i = 0; i < lines.Length; i++)
            page.InsertText(new Point(50, 120 + i * 24), lines[i], fontSize: 11, fontName: "korea");
    }

    private static void AddPreexistingItems(Document doc)
    {
        // 다른 프로그램이 남긴 메모 주석이 있다고 가정. 이 앱이 저장해도 지워지면 안 된다.
        var plan = doc[PlanSetPages.FloorPlan];
        var note = plan.AddTextAnnot(new Point(300, 300), "Existing reviewer note — keep me.");
        note.SetInfo(info: new AnnotInfo { Content = "Existing reviewer note — keep me.", Title = "Other Reviewer", Subject = "Existing Note" });
        note.Update();

        plan.InsertLink(new LinkInfo
        {
            Kind = LinkType.LINK_URI,
            From = new Rect(100, ArchDHeight - 140, 400, ArchDHeight - 110),
            Uri = "https://example.com/project-spec",
        });

        var spec = doc[PlanSetPages.KoreanSpec];
        spec.InsertLink(new LinkInfo
        {
            Kind = LinkType.LINK_GOTO,
            From = new Rect(50, 195, 400, 215),
            Page = PlanSetPages.FloorPlan,
            To = new Point(0, 0),
        });
    }
}
