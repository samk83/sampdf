using DrawingReview.Compare.Model;
using OpenCvSharp;

namespace DrawingReview.Compare;

/// <summary>
/// 자동 정렬 (기획서 9장): 이전 도면을 개정 도면에 맞추는 이동·회전·배율을 찾는다.
/// 1) 같은 크기이고 이미 잘 겹치면 그대로 쓴다.
/// 2) 아니면 특징점(ORB) 매칭 + RANSAC으로 이동·회전·균일 배율을 추정한다.
/// 3) ECC로 미세 조정하고, 겹침 정도(Quality)로 결과를 검증한다. 나빠지면 조정을 버린다.
/// </summary>
public static class Aligner
{
    private const int WorkLongEdge = 1600;
    private const double IdentityGoodEnough = 0.97;

    public static AlignmentResult Align(PageSource oldPage, PageSource newPage)
    {
        var zoom = Math.Min(WorkLongEdge / Math.Max(newPage.Width, newPage.Height), WorkLongEdge / Math.Max(oldPage.Width, oldPage.Height));
        using var oldGray = ImageOps.ToGray(oldPage.Render(zoom));
        using var newGray = ImageOps.ToGray(newPage.Render(zoom));

        var sameSize = Math.Abs(oldPage.Width - newPage.Width) < 1 && Math.Abs(oldPage.Height - newPage.Height) < 1;
        if (sameSize)
        {
            var q = Quality(oldGray, newGray, AlignmentTransform.Identity, zoom);
            if (q >= IdentityGoodEnough)
                return new AlignmentResult(AlignmentTransform.Identity, AlignmentMethod.Identity, q, "Pages already line up.");
        }

        // 1차 추정: 특징점 → 실패하면 페이지 크기 비율
        string note;
        AlignmentTransform initial;
        if (TryFeatureMatch(oldGray, newGray, zoom, out var feature, out var inliers))
        {
            initial = feature;
            note = $"Feature match ({inliers} points)";
        }
        else
        {
            initial = sameSize ? AlignmentTransform.Identity
                : new AlignmentTransform(newPage.Width / oldPage.Width, 0, 0, 0, newPage.Height / oldPage.Height, 0);
            note = "Feature match failed; started from page size";
        }
        var initialQuality = Quality(oldGray, newGray, initial, zoom);

        // 2차: ECC 미세 조정
        var refined = RefineEcc(oldGray, newGray, initial, zoom);
        if (refined is { } r)
        {
            var q = Quality(oldGray, newGray, r, zoom);
            if (q >= initialQuality - 1e-3)
                return new AlignmentResult(r, AlignmentMethod.Automatic, q, note + " + ECC refinement");
        }
        return new AlignmentResult(initial, AlignmentMethod.Automatic, initialQuality, note);
    }

    /// <summary>수동 2점 정렬 결과를 같은 기준으로 품질 평가해 돌려준다.</summary>
    public static AlignmentResult Manual(PageSource oldPage, PageSource newPage, AlignmentTransform transform)
    {
        var zoom = Math.Min(WorkLongEdge / Math.Max(newPage.Width, newPage.Height), WorkLongEdge / Math.Max(oldPage.Width, oldPage.Height));
        using var oldGray = ImageOps.ToGray(oldPage.Render(zoom));
        using var newGray = ImageOps.ToGray(newPage.Render(zoom));
        return new AlignmentResult(transform, AlignmentMethod.Manual, Quality(oldGray, newGray, transform, zoom), "Manual 2-point alignment");
    }

    /// <summary>
    /// 겹침 정도: 각 도면의 그림 픽셀 중 상대 도면 그림(2픽셀 허용) 위에 놓이는 비율의 작은 쪽.
    /// 내용이 일부 바뀌어도 대부분 같으면 높게 나온다.
    /// </summary>
    internal static double Quality(Mat oldGray, Mat newGray, AlignmentTransform t, double zoom)
    {
        using var warped = ImageOps.Warp(oldGray, t, zoom, newGray.Size());
        // 진한 선만 본다: 옅은 회색 점선은 반 픽셀 차이에도 판정이 흔들린다.
        using var inkOld = ImageOps.Ink(warped, 160);
        using var inkNew = ImageOps.Ink(newGray, 160);
        var nOld = Cv2.CountNonZero(inkOld);
        var nNew = Cv2.CountNonZero(inkNew);
        if (nOld == 0 || nNew == 0) return nOld == nNew ? 1 : 0;
        using var dilOld = ImageOps.Dilate(inkOld, 2);
        using var dilNew = ImageOps.Dilate(inkNew, 2);
        using var a = new Mat();
        using var b = new Mat();
        Cv2.BitwiseAnd(inkNew, dilOld, a);
        Cv2.BitwiseAnd(inkOld, dilNew, b);
        return Math.Min(Cv2.CountNonZero(a) / (double)nNew, Cv2.CountNonZero(b) / (double)nOld);
    }

    private static bool TryFeatureMatch(Mat oldGray, Mat newGray, double zoom, out AlignmentTransform transform, out int inlierCount)
    {
        transform = AlignmentTransform.Identity;
        inlierCount = 0;
        using var orb = ORB.Create(5000);
        using var dOld = new Mat();
        using var dNew = new Mat();
        orb.DetectAndCompute(oldGray, null, out var kOld, dOld);
        orb.DetectAndCompute(newGray, null, out var kNew, dNew);
        if (kOld.Length < 10 || kNew.Length < 10 || dOld.Empty() || dNew.Empty()) return false;

        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        var matches = matcher.Match(dOld, dNew);
        if (matches.Length < 12) return false;

        var src = matches.Select(m => kOld[m.QueryIdx].Pt).ToArray();
        var dst = matches.Select(m => kNew[m.TrainIdx].Pt).ToArray();
        using var srcMat = Mat.FromArray(src);
        using var dstMat = Mat.FromArray(dst);
        using var inliers = new Mat();
        using var m = Cv2.EstimateAffinePartial2D(srcMat, dstMat, inliers, RobustEstimationAlgorithms.RANSAC, 3.0);
        if (m == null || m.Empty()) return false;
        inlierCount = Cv2.CountNonZero(inliers);
        if (inlierCount < 12 || inlierCount < matches.Length * 0.15) return false;

        // 픽셀 단위 변환 → pt 단위 (이동량만 확대율로 나눈다)
        transform = new AlignmentTransform(
            m.At<double>(0, 0), m.At<double>(0, 1), m.At<double>(0, 2) / zoom,
            m.At<double>(1, 0), m.At<double>(1, 1), m.At<double>(1, 2) / zoom);
        return true;
    }

    private static AlignmentTransform? RefineEcc(Mat oldGray, Mat newGray, AlignmentTransform initial, double zoom)
    {
        // ECC는 "개정 도면 좌표 → 이전 도면 좌표" 변환을 찾으므로 역변환으로 시작한다.
        AlignmentTransform inv;
        try { inv = initial.Inverse(); }
        catch (InvalidOperationException) { return null; }

        using var tmpl = new Mat();
        using var input = new Mat();
        Cv2.GaussianBlur(newGray, tmpl, new Size(7, 7), 0);
        Cv2.GaussianBlur(oldGray, input, new Size(7, 7), 0);
        using var warp = new Mat(2, 3, MatType.CV_32FC1);
        warp.Set(0, 0, (float)inv.A); warp.Set(0, 1, (float)inv.B); warp.Set(0, 2, (float)(inv.C * zoom));
        warp.Set(1, 0, (float)inv.D); warp.Set(1, 1, (float)inv.E); warp.Set(1, 2, (float)(inv.F * zoom));
        try
        {
            Cv2.FindTransformECC(tmpl, input, warp, MotionTypes.Affine,
                new TermCriteria(CriteriaTypes.Count | CriteriaTypes.Eps, 80, 1e-5), null, 5);
        }
        catch (OpenCVException)
        {
            return null; // 수렴하지 않음 → 1차 추정 유지
        }
        var found = new AlignmentTransform(
            warp.At<float>(0, 0), warp.At<float>(0, 1), warp.At<float>(0, 2) / zoom,
            warp.At<float>(1, 0), warp.At<float>(1, 1), warp.At<float>(1, 2) / zoom);
        try { return found.Inverse(); }
        catch (InvalidOperationException) { return null; }
    }
}
