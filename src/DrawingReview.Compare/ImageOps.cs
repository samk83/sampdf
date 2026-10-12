using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using OpenCvSharp;

namespace DrawingReview.Compare;

/// <summary>비교에 쓰는 이미지 처리 도우미 (OpenCV).</summary>
internal static class ImageOps
{
    public static Mat ToGray(RenderedImage img)
    {
        using var rgb = Mat.FromPixelData(img.Height, img.Width, MatType.CV_8UC3, img.Pixels, img.Stride);
        var gray = new Mat();
        Cv2.CvtColor(rgb, gray, ColorConversionCodes.RGB2GRAY);
        return gray;
    }

    /// <summary>이전 도면 이미지를 개정 도면 좌표계로 옮긴다. 바깥은 흰색.</summary>
    public static Mat Warp(Mat oldGray, AlignmentTransform t, double zoom, Size size)
    {
        // pt 단위 변환을 픽셀 단위로: 선형 부분은 같고 이동량만 확대율을 곱한다.
        using var m = new Mat(2, 3, MatType.CV_64FC1);
        m.Set(0, 0, t.A); m.Set(0, 1, t.B); m.Set(0, 2, t.C * zoom);
        m.Set(1, 0, t.D); m.Set(1, 1, t.E); m.Set(1, 2, t.F * zoom);
        var dst = new Mat();
        Cv2.WarpAffine(oldGray, dst, m, size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));
        return dst;
    }

    /// <summary>어두운 픽셀(그림) = 255, 배경 = 0.</summary>
    public static Mat Ink(Mat gray, int threshold)
    {
        var mask = new Mat();
        Cv2.Threshold(gray, mask, threshold, 255, ThresholdTypes.BinaryInv);
        return mask;
    }

    public static Mat Dilate(Mat mask, int radius)
    {
        var dst = new Mat();
        if (radius <= 0) { mask.CopyTo(dst); return dst; }
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * radius + 1, 2 * radius + 1));
        Cv2.Dilate(mask, dst, kernel);
        return dst;
    }

    /// <summary>
    /// image가 other보다 "확실히 어두운" 그림 픽셀 = 255.
    /// other는 반경 radius 안의 가장 어두운 값(최소값 필터)과 비교하므로 radius 픽셀까지의 위치 차이는 무시된다.
    /// </summary>
    public static Mat DarkerThanNeighborhood(Mat image, Mat other, int radius, int delta, int inkThreshold)
    {
        using var otherMin = new Mat();
        if (radius > 0)
        {
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * radius + 1, 2 * radius + 1));
            Cv2.Erode(other, otherMin, kernel); // 밝기 영상에서 침식 = 주변 최소(가장 어두운) 값
        }
        else other.CopyTo(otherMin);
        using var diff = new Mat();
        Cv2.Subtract(otherMin, image, diff); // 음수는 0 (포화)
        using var darker = new Mat();
        Cv2.Threshold(diff, darker, delta, 255, ThresholdTypes.Binary);
        using var ink = Ink(image, inkThreshold);
        var result = new Mat();
        Cv2.BitwiseAnd(darker, ink, result);
        return result;
    }

    /// <summary>minPixels보다 작은 점 덩어리를 지운다 (렌더링 먼지).</summary>
    public static void RemoveSpecks(Mat mask, int minPixels)
    {
        if (minPixels <= 1) return;
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var n = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids, PixelConnectivity.Connectivity8);
        if (n <= 1) return;
        var remove = new bool[n];
        var any = false;
        for (int i = 1; i < n; i++)
        {
            if (stats.At<int>(i, (int)ConnectedComponentsTypes.Area) < minPixels) { remove[i] = true; any = true; }
        }
        if (!any) return;
        labels.GetArray(out int[] lab);
        mask.GetArray(out byte[] data);
        for (int i = 0; i < lab.Length; i++)
            if (remove[lab[i]]) data[i] = 0;
        mask.SetArray(data);
    }

    public static Rect ToPixelRect(PageRect r, double zoom, Size size)
    {
        int x0 = Math.Clamp((int)Math.Floor(r.X0 * zoom), 0, size.Width);
        int y0 = Math.Clamp((int)Math.Floor(r.Y0 * zoom), 0, size.Height);
        int x1 = Math.Clamp((int)Math.Ceiling(r.X1 * zoom), 0, size.Width);
        int y1 = Math.Clamp((int)Math.Ceiling(r.Y1 * zoom), 0, size.Height);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }
}

/// <summary>가까운 차이 픽셀을 한 변경 영역으로 묶는다.</summary>
internal static class Grouping
{
    public readonly record struct DiffGroup(PageRect Bounds, int Added, int Removed);

    public static List<DiffGroup> Group(Mat added, Mat removed, int groupPixels, double zoom)
    {
        using var combined = new Mat();
        Cv2.BitwiseOr(added, removed, combined);
        if (Cv2.CountNonZero(combined) == 0) return [];

        // 묶기 거리만큼 넓힌 그림에서 연결된 덩어리 = 한 영역
        using var spread = ImageOps.Dilate(combined, Math.Max(1, groupPixels / 2));
        using var labels = new Mat();
        var n = Cv2.ConnectedComponents(spread, labels, PixelConnectivity.Connectivity8);

        labels.GetArray(out int[] lab);
        added.GetArray(out byte[] a);
        removed.GetArray(out byte[] r);
        int w = added.Width;
        var minX = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var minY = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var maxX = new int[n];
        var maxY = new int[n];
        var addCount = new int[n];
        var remCount = new int[n];
        for (int i = 0; i < lab.Length; i++)
        {
            bool isA = a[i] != 0, isR = r[i] != 0;
            if (!isA && !isR) continue;
            var l = lab[i];
            int x = i % w, y = i / w;
            if (x < minX[l]) minX[l] = x;
            if (y < minY[l]) minY[l] = y;
            if (x > maxX[l]) maxX[l] = x;
            if (y > maxY[l]) maxY[l] = y;
            if (isA) addCount[l]++;
            if (isR) remCount[l]++;
        }

        var result = new List<DiffGroup>();
        for (int l = 1; l < n; l++)
        {
            if (addCount[l] + remCount[l] == 0) continue;
            // 픽셀 경계를 포함하도록 1픽셀 넓힌다
            var bounds = new PageRect(minX[l] / zoom, minY[l] / zoom, (maxX[l] + 1) / zoom, (maxY[l] + 1) / zoom);
            result.Add(new DiffGroup(bounds, addCount[l], remCount[l]));
        }
        return result;
    }
}
