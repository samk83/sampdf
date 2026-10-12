using System.Diagnostics;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using OpenCvSharp;

namespace DrawingReview.Compare;

/// <summary>
/// 비교할 페이지 하나: 크기(pt)와 "확대율을 주면 그 해상도로 렌더링하는 함수".
/// 엔진 호출 순서(스레드)는 호출하는 쪽이 책임진다.
/// </summary>
public sealed record PageSource(double Width, double Height, Func<double, RenderedImage> Render, Func<IReadOnlyList<TextWord>>? Words = null)
{
    public static PageSource From(IPdfDocument doc, int pageIndex)
    {
        var info = doc.GetPageInfo(pageIndex);
        return new PageSource(info.Width, info.Height, zoom => doc.RenderPage(pageIndex, zoom, includeMarkups: false), () => doc.GetWords(pageIndex));
    }
}

/// <summary>
/// PDF 페이지 비교 엔진 (기획서 7장 PDF 규칙):
/// 정렬 → 같은 해상도로 렌더링 → 허용 오차를 둔 그림 차이 → 잡음 제거 → 가까운 차이 묶기 → 변경 영역.
/// 결과는 "변경 후보"이며 공학적 판단을 하지 않는다.
/// </summary>
public static class PageComparer
{
    public static CompareResult Compare(PageSource oldPage, PageSource newPage, CompareSettings? settings = null, AlignmentResult? alignment = null)
    {
        settings ??= new CompareSettings();
        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();
        alignment ??= Aligner.Align(oldPage, newPage);
        if (!alignment.IsReliable)
            warnings.Add($"Automatic alignment is uncertain (quality {alignment.Quality:0.00}). Check the overlay and use manual 2-point alignment if needed.");
        if (Math.Abs(oldPage.Width - newPage.Width) > 1 || Math.Abs(oldPage.Height - newPage.Height) > 1)
            warnings.Add($"Page sizes differ ({oldPage.Width:0}×{oldPage.Height:0} pt vs {newPage.Width:0}×{newPage.Height:0} pt).");

        var zoom = settings.Dpi / 72.0;
        using var newGray = ImageOps.ToGray(newPage.Render(zoom));
        using var oldGray = ImageOps.ToGray(oldPage.Render(zoom));
        using var oldWarped = ImageOps.Warp(oldGray, alignment.Transform, zoom, newGray.Size());

        // 추가 = 개정 도면의 그림 픽셀이 "이전 도면의 주변(허용 오차 안) 가장 어두운 값"보다도 확실히 어두운 곳.
        // 흑백으로 자르지 않고 밝기 차이로 보므로, 얇은 회색 선이 반 픽셀 움직여 흐려지는 렌더링 차이를 걸러낸다.
        using var added = ImageOps.DarkerThanNeighborhood(newGray, oldWarped, settings.TolerancePixels, settings.ContrastDelta, settings.InkThreshold);
        using var removed = ImageOps.DarkerThanNeighborhood(oldWarped, newGray, settings.TolerancePixels, settings.ContrastDelta, settings.InkThreshold);

        // PDF 글자 비교: 짝이 없는 단어(내용이나 위치가 바뀐 글자)를 변경으로 칠한다.
        // 작은 글씨의 치수 변경은 픽셀 차이가 적어 놓치기 쉬우므로 글자 정보로 보강한다 (기획서 3장 "텍스트 추출 활용").
        if (oldPage.Words != null && newPage.Words != null)
        {
            var (addedWords, removedWords) = TextDiff.Compare(oldPage.Words(), newPage.Words(), alignment.Transform);
            foreach (var w in addedWords) added[ImageOps.ToPixelRect(w, zoom, added.Size())].SetTo(Scalar.All(255));
            foreach (var w in removedWords) removed[ImageOps.ToPixelRect(w, zoom, removed.Size())].SetTo(Scalar.All(255));
        }

        foreach (var r in settings.IgnoreRegions)
        {
            var rect = ImageOps.ToPixelRect(r, zoom, added.Size());
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            added[rect].SetTo(Scalar.All(0));
            removed[rect].SetTo(Scalar.All(0));
        }

        ImageOps.RemoveSpecks(added, settings.NoisePixels);
        ImageOps.RemoveSpecks(removed, settings.NoisePixels);

        var inverse = alignment.Transform.Inverse();
        var regions = Grouping.Group(added, removed, (int)Math.Ceiling(settings.GroupDistancePt * zoom), zoom)
            .Select(g =>
            {
                var total = g.Added + g.Removed;
                var kind = g.Added > 0 && g.Removed > 0 ? ChangeKind.Modified : g.Added > 0 ? ChangeKind.Added : ChangeKind.Removed;
                var confidence = total < settings.LowConfidencePixels || !alignment.IsReliable ? ChangeConfidence.Low : ChangeConfidence.High;
                return (g.Bounds, kind, g.Added, g.Removed, confidence);
            })
            .OrderBy(g => Math.Round(g.Bounds.Y0 / 50)).ThenBy(g => g.Bounds.X0) // 위에서 아래, 왼쪽에서 오른쪽
            .Select((g, i) => new ChangeRegion(i + 1, g.kind, g.Bounds, inverse.Apply(g.Bounds), g.Added, g.Removed, g.confidence))
            .ToList();

        return new CompareResult(regions, alignment, settings, newPage.Width, newPage.Height, sw.Elapsed, warnings);
    }

    /// <summary>
    /// 한 영역을 더 잘게 나눈다 (기획서 5장 Split): 묶기 거리를 줄여 다시 비교하고 그 영역 안의 결과만 돌려준다.
    /// </summary>
    public static IReadOnlyList<ChangeRegion> Split(PageSource oldPage, PageSource newPage, CompareSettings settings, AlignmentResult alignment, PageRect region)
    {
        var fine = settings with { GroupDistancePt = Math.Max(1, settings.GroupDistancePt / 4) };
        var result = Compare(oldPage, newPage, fine, alignment);
        return result.Regions.Where(r => region.Inflate(1).Contains(r.Bounds)).ToList();
    }

    /// <summary>여러 영역을 하나로 합친다 (기획서 5장 Merge).</summary>
    public static ChangeRegion Merge(IReadOnlyList<ChangeRegion> regions, int number)
    {
        if (regions.Count == 0) throw new ArgumentException("Nothing to merge.", nameof(regions));
        var b = regions.Select(r => r.Bounds).Aggregate((a, c) => new PageRect(Math.Min(a.X0, c.X0), Math.Min(a.Y0, c.Y0), Math.Max(a.X1, c.X1), Math.Max(a.Y1, c.Y1)));
        var bo = regions.Select(r => r.BoundsInOld).Aggregate((a, c) => new PageRect(Math.Min(a.X0, c.X0), Math.Min(a.Y0, c.Y0), Math.Max(a.X1, c.X1), Math.Max(a.Y1, c.Y1)));
        int added = regions.Sum(r => r.AddedPixels), removed = regions.Sum(r => r.RemovedPixels);
        var kind = added > 0 && removed > 0 ? ChangeKind.Modified : added > 0 ? ChangeKind.Added : ChangeKind.Removed;
        var confidence = regions.Any(r => r.Confidence == ChangeConfidence.High) ? ChangeConfidence.High : ChangeConfidence.Low;
        return new ChangeRegion(number, kind, b, bo, added, removed, confidence);
    }

    /// <summary>렌더링 결과를 PNG 바이트로 (비교 이미지 내보내기).</summary>
    public static byte[] EncodePng(RenderedImage image)
    {
        using var rgb = Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC3, image.Pixels, image.Stride);
        using var bgr = new Mat();
        Cv2.CvtColor(rgb, bgr, ColorConversionCodes.RGB2BGR);
        Cv2.ImEncode(".png", bgr, out var bytes);
        return bytes;
    }

    /// <summary>
    /// 겹쳐 보기 그림: 이전 도면만 있는 선 = 빨강, 개정 도면만 있는 선 = 파랑, 공통 = 진회색 (기획서 9장 기본 색).
    /// </summary>
    public static RenderedImage RenderOverlay(PageSource oldPage, PageSource newPage, AlignmentTransform transform, double zoom,
        (byte R, byte G, byte B)? oldColor = null, (byte R, byte G, byte B)? newColor = null, int inkThreshold = 200, bool showCommon = true)
    {
        var oc = oldColor ?? (220, 40, 40);
        var nc = newColor ?? (30, 90, 230);
        using var newGray = ImageOps.ToGray(newPage.Render(zoom));
        using var oldGray = ImageOps.ToGray(oldPage.Render(zoom));
        using var oldWarped = ImageOps.Warp(oldGray, transform, zoom, newGray.Size());
        using var inkNew = ImageOps.Ink(newGray, inkThreshold);
        using var inkOld = ImageOps.Ink(oldWarped, inkThreshold);

        int w = newGray.Width, h = newGray.Height, stride = w * 3;
        var pixels = new byte[stride * h];
        Array.Fill(pixels, (byte)255);
        inkNew.GetArray(out byte[] n);
        inkOld.GetArray(out byte[] o);
        for (int i = 0; i < n.Length; i++)
        {
            bool a = n[i] != 0, b = o[i] != 0;
            if (!a && !b) continue;
            if (a && b && !showCommon) continue; // 차이만 보기: 공통 선은 그리지 않음
            var (r, g, bl) = a && b ? ((byte)60, (byte)60, (byte)60) : a ? nc : oc;
            pixels[i * 3] = r;
            pixels[i * 3 + 1] = g;
            pixels[i * 3 + 2] = bl;
        }
        return new RenderedImage(w, h, stride, pixels, 0, 0, zoom);
    }
}
