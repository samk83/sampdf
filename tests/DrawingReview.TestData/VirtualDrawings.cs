using PdfSharp.Drawing;
using PdfSharp.Pdf;

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
        using var doc = new PdfDocument();

        PdfSketch.NewPage(doc, ArchDWidth, ArchDHeight);
        DrawFloorPlan(doc, PlanSetPages.FloorPlan, ArchDWidth, ArchDHeight, "A-101", "FIRST FLOOR PLAN / 1층 평면도");

        PdfSketch.NewPage(doc, ArchDHeight, ArchDWidth);
        DrawFloorPlan(doc, PlanSetPages.Rotated90, ArchDHeight, ArchDWidth, "A-102", "ROOF FRAMING PLAN / 지붕 구조 평면도");
        doc.Pages[PlanSetPages.Rotated90].Rotate = 90;

        PdfSketch.NewPage(doc, A3Width, A3Height);
        DrawFloorPlan(doc, PlanSetPages.CroppedA3, A3Width, A3Height, "S-301", "SECTION DETAIL / 단면 상세");
        // 왼쪽 위 기준 (60, 40) ~ (A3W-30, A3H-50) → PDF 좌표(왼쪽 아래 원점)
        doc.Pages[PlanSetPages.CroppedA3].CropBox = new PdfRectangle(new XPoint(60, 50), new XPoint(A3Width - 30, A3Height - 40));

        PdfSketch.NewPage(doc, A4Width, A4Height);
        DrawSpecSheet(doc, PlanSetPages.KoreanSpec);

        PdfSketch.NewPage(doc, A3Width, A3Height);
        DrawFloorPlan(doc, PlanSetPages.Rotated180, A3Width, A3Height, "M-201", "MECHANICAL PLAN / 기계 평면도");
        doc.Pages[PlanSetPages.Rotated180].Rotate = 180;

        PdfSketch.NewPage(doc, A3Height, A3Width);
        DrawFloorPlan(doc, PlanSetPages.Rotated270, A3Height, A3Width, "E-401", "ELECTRICAL PLAN / 전기 평면도");
        doc.Pages[PlanSetPages.Rotated270].Rotate = 270;

        AddPreexistingItems(doc);

        var arch = doc.Outlines.Add("Architectural", doc.Pages[0], true);
        arch.Outlines.Add("A-101 First Floor Plan", doc.Pages[0]);
        arch.Outlines.Add("A-102 Roof Framing Plan", doc.Pages[1]);
        var structural = doc.Outlines.Add("Structural", doc.Pages[2], true);
        structural.Outlines.Add("S-301 Section Detail", doc.Pages[2]);
        doc.Outlines.Add("General Notes / 일반 사항", doc.Pages[3]);
        return PdfTools.ToBytes(doc);
    }

    /// <summary>성능 측정용: 선이 매우 많은 대형 도면 1장.</summary>
    public static byte[] CreateDenseVectorSheet(int gridDivisions = 160)
    {
        using var doc = new PdfDocument();
        const float w = 42 * 72, h = 30 * 72; // ARCH E1 가로
        var page = PdfSketch.NewPage(doc, w, h);
        var shape = new PdfSketch(doc, page);
        var rnd = new Random(42);
        // 격자 + 셀마다 대각선 해치: (n+1)*2 + n*n*2 개 선
        for (int i = 0; i <= gridDivisions; i++)
        {
            float x = 50 + i * (w - 100) / gridDivisions, y = 50 + i * (h - 100) / gridDivisions;
            shape.DrawLine(x, 50, x, h - 50);
            shape.DrawLine(50, y, w - 50, y);
        }
        shape.Finish([0, 0, 0], 0.3);
        float cw = (w - 100) / gridDivisions, ch = (h - 100) / gridDivisions;
        for (int i = 0; i < gridDivisions; i++)
            for (int j = 0; j < gridDivisions; j++)
            {
                float x = 50 + i * cw, y = 50 + j * ch;
                shape.DrawLine(x, y, x + cw, y + ch);
                if (rnd.Next(3) == 0) shape.DrawLine(x + cw, y, x, y + ch);
            }
        shape.Finish([0.2, 0.2, 0.6], 0.15);
        shape.InsertText(80, h - 70, "DENSE VECTOR TEST SHEET", 36);
        shape.Commit();
        return PdfTools.ToBytes(doc);
    }

    private static void DrawFloorPlan(PdfDocument doc, int pageIndex, float w, float h, string sheet, string title)
    {
        var page = doc.Pages[pageIndex];
        var s = new PdfSketch(doc, page);
        float m = Math.Min(w, h) * 0.03f;

        // 테두리
        s.DrawRect(m, m, w - m, h - m);
        s.Finish([0, 0, 0], 2);

        // 그리드 (A~F, 1~8) — 점선
        float left = m * 3, top = m * 3, right = w - m * 3, bottom = h - m * 7;
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.DrawLine(x, top, x, bottom);
        }
        for (int j = 0; j < 6; j++)
        {
            float y = top + j * (bottom - top) / 5;
            s.DrawLine(left, y, right, y);
        }
        s.Finish([0.5, 0.5, 0.5], 0.5, dashes: "[6 3] 0");

        // 그리드 버블
        float r = m * 0.6f;
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.DrawCircle(x, top - r * 1.5f, r);
        }
        s.Finish([0, 0, 0], 1);
        for (int i = 0; i < 8; i++)
        {
            float x = left + i * (right - left) / 7;
            s.InsertText(x - r * 0.35f, top - r * 1.2f, (i + 1).ToString(), r);
        }

        // 벽체 (두꺼운 선)
        s.DrawRect(left + 20, top + 20, right - 20, bottom - 20);
        s.DrawLine((left + right) / 2, top + 20, (left + right) / 2, bottom - 20);
        s.DrawLine(left + 20, (top + bottom) / 2, (left + right) / 2, (top + bottom) / 2);
        s.Finish([0, 0, 0], 4);

        // 트러스·보 표기 (검색 테스트용 영문 키워드)
        s.InsertText(left + 40, top + 60, "NEW TRUSS T-1 @ 600 O.C.", m * 0.5f);
        s.InsertText(left + 40, top + 60 + m, "EXISTING BEAM W12x26", m * 0.5f);

        // 제목란 (오른쁜 아래)
        double tbX0 = w - m - Math.Min(w * 0.3f, 900), tbY0 = h - m * 6, tbX1 = w - m, tbY1 = h - m;
        double tbW = tbX1 - tbX0, tbH = tbY1 - tbY0;
        s.DrawRect(tbX0, tbY0, tbX1, tbY1);
        s.DrawLine(tbX0, tbY0 + tbH / 2, tbX1, tbY0 + tbH / 2);
        s.Finish([0, 0, 0], 1.5);
        s.InsertText(tbX0 + 10, tbY0 + tbH * 0.35, "PROJECT: SAMPLE OFFICE BUILDING", m * 0.45f);
        s.InsertText(tbX0 + 10, tbY0 + tbH * 0.85, $"SHEET {sheet}", m * 0.6f);
        s.Commit();

        // 한글 제목은 CJK 글꼴 참조(비포함)로 넣는다: 파일 크기를 작게 유지한다.
        KoreanText.Insert(doc, pageIndex, tbX0 + tbW * 0.45, tbY0 + tbH * 0.85, title, m * 0.4f);
    }

    private static void DrawSpecSheet(PdfDocument doc, int pageIndex)
    {
        KoreanText.Insert(doc, pageIndex, 50, 70, "GENERAL NOTES / 일반 사항", 18);
        string[] lines =
        [
            "1. 모든 치수는 현장에서 확인한다. VERIFY ALL DIMENSIONS IN FIELD.",
            "2. 신설 트러스와 기존 보 사이의 간격을 확인한다.",
            "   VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM.",
            "3. 구조 도면과 기계 도면의 간섭 여부를 검토한다.",
            "4. See sheet A-101 for floor plan. 평면도는 A-101 참조.",
        ];
        for (int i = 0; i < lines.Length; i++)
            KoreanText.Insert(doc, pageIndex, 50, 120 + i * 24, lines[i], 11);
    }

    private static void AddPreexistingItems(PdfDocument doc)
    {
        // 다른 프로그램이 남긴 메모 주석(+ 말풍선 창)이 있다고 가정. 이 앱이 저장해도 지워지면 안 된다.
        var plan = doc.Pages[PlanSetPages.FloorPlan];
        var note = Annot(doc, plan, "/Text", 300, ArchDHeight - 320, 320, ArchDHeight - 300);
        note.Elements.SetString("/Contents", "Existing reviewer note — keep me.");
        note.Elements.SetString("/T", "Other Reviewer");
        note.Elements.SetString("/Subj", "Existing Note");
        note.Elements.SetName("/Name", "/Note");
        var popup = Annot(doc, plan, "/Popup", 320, ArchDHeight - 420, 500, ArchDHeight - 300);
        popup.Elements.SetReference("/Parent", note);
        popup.Elements.SetBoolean("/Open", false);
        note.Elements.SetReference("/Popup", popup);

        var uri = Annot(doc, plan, "/Link", 100, 110, 400, 140);
        var action = new PdfDictionary(doc);
        action.Elements.SetName("/S", "/URI");
        action.Elements.SetString("/URI", "https://example.com/project-spec");
        uri.Elements["/A"] = action;
        uri.Elements["/Border"] = Numbers(doc, 0, 0, 0);

        var spec = doc.Pages[PlanSetPages.KoreanSpec];
        var goTo = Annot(doc, spec, "/Link", 50, A4Height - 215, 400, A4Height - 195);
        var dest = new PdfArray(doc);
        dest.Elements.Add(plan.Reference!);
        dest.Elements.Add(new PdfName("/XYZ"));
        dest.Elements.Add(new PdfInteger(0));
        dest.Elements.Add(new PdfReal(ArchDHeight));
        dest.Elements.Add(new PdfInteger(0));
        var goToAction = new PdfDictionary(doc);
        goToAction.Elements.SetName("/S", "/GoTo");
        goToAction.Elements["/D"] = dest;
        goTo.Elements["/A"] = goToAction;
        goTo.Elements["/Border"] = Numbers(doc, 0, 0, 0);
    }

    /// <summary>주석 사전을 만들어 페이지 /Annots에 넣는다. 사각형은 PDF 좌표(왼쪽 아래 원점).</summary>
    private static PdfDictionary Annot(PdfDocument doc, PdfPage page, string subtype, double x0, double y0, double x1, double y1)
    {
        var annot = new PdfDictionary(doc);
        annot.Elements.SetName("/Type", "/Annot");
        annot.Elements.SetName("/Subtype", subtype);
        annot.Elements["/Rect"] = Numbers(doc, x0, y0, x1, y1);
        annot.Elements.SetInteger("/F", 4);
        annot.Elements.SetReference("/P", page);
        doc.Internals.AddObject(annot);
        if (!page.Elements.ContainsKey("/Annots")) page.Elements["/Annots"] = new PdfArray(doc);
        ((PdfArray)page.Elements["/Annots"]!).Elements.Add(annot.Reference!);
        return annot;
    }

    private static PdfArray Numbers(PdfDocument doc, params double[] values)
    {
        var array = new PdfArray(doc);
        foreach (var v in values) array.Elements.Add(new PdfReal(v));
        return array;
    }
}
