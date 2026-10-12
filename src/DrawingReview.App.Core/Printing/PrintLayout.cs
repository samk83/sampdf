namespace DrawingReview.App.Core.Printing;

public enum PrintScaling
{
    /// <summary>용지에 맞춤 (축척이 바뀜).</summary>
    FitToPaper,
    /// <summary>실제 크기 (100%). 용지보다 크면 잘린다 — 미리보기에 경고한다.</summary>
    ActualSize,
}

/// <summary>용지 위 배치 결과. Scale은 PDF pt → 인쇄 단위(1/96 inch) 배율, Rotate는 90° 돌려 찍는지.</summary>
public sealed record PrintPlacement(double Scale, double OffsetX, double OffsetY, bool Rotate, bool Clipped)
{
    /// <summary>실제 크기 대비 백분율.</summary>
    public double Percent => Scale / PrintLayout.PtToDip * 100;
}

/// <summary>
/// 인쇄 배치 계산 (기획서 13장: 실제 크기 인쇄와 용지 맞춤을 구분).
/// 단위: 페이지는 PDF pt(1/72 inch), 용지는 WPF 단위(1/96 inch).
/// </summary>
public static class PrintLayout
{
    public const double PtToDip = 96.0 / 72.0;

    public static PrintPlacement Place(double pageWidthPt, double pageHeightPt, double paperWidth, double paperHeight, PrintScaling scaling, bool autoRotate = true)
    {
        // 페이지와 용지의 방향(가로/세로)이 다르면 90° 돌려 찍는다.
        var rotate = autoRotate && (pageWidthPt > pageHeightPt) != (paperWidth > paperHeight) && Math.Abs(pageWidthPt - pageHeightPt) > 1;
        var w = (rotate ? pageHeightPt : pageWidthPt) * PtToDip;
        var h = (rotate ? pageWidthPt : pageHeightPt) * PtToDip;
        double scale = scaling == PrintScaling.FitToPaper ? Math.Min(paperWidth / w, paperHeight / h) : 1;
        var clipped = scaling == PrintScaling.ActualSize && (w > paperWidth + 0.5 || h > paperHeight + 0.5);
        var offsetX = (paperWidth - w * scale) / 2;
        var offsetY = (paperHeight - h * scale) / 2;
        return new PrintPlacement(scale * PtToDip, offsetX, offsetY, rotate, clipped);
    }

    /// <summary>"1-3, 5" 같은 페이지 범위를 0부터 시작하는 번호로. 잘못된 부분은 무시한다.</summary>
    public static IReadOnlyList<int> ParseRange(string text, int pageCount)
    {
        var result = new SortedSet<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split('-', StringSplitOptions.TrimEntries);
            if (bits.Length == 1 && int.TryParse(bits[0], out var single)) Add(single, single);
            else if (bits.Length == 2 && int.TryParse(bits[0], out var a) && int.TryParse(bits[1], out var b)) Add(Math.Min(a, b), Math.Max(a, b));
        }
        return result.ToList();

        void Add(int from, int to)
        {
            for (int p = Math.Max(1, from); p <= Math.Min(pageCount, to); p++) result.Add(p - 1);
        }
    }
}
