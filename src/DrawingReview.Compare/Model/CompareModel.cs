using DrawingReview.Engine.Geometry;

namespace DrawingReview.Compare.Model;

/// <summary>
/// 이전 도면 페이지 좌표(pt) → 개정 도면 페이지 좌표(pt) 변환 (2x3 아핀 행렬).
/// x' = A·x + B·y + C,  y' = D·x + E·y + F
/// 변경 영역과 Revision Cloud는 개정 도면 좌표로 저장하고, 이 변환으로 이전 도면 위치를 계산한다.
/// </summary>
public readonly record struct AlignmentTransform(double A, double B, double C, double D, double E, double F)
{
    public static readonly AlignmentTransform Identity = new(1, 0, 0, 0, 1, 0);

    public PagePoint Apply(PagePoint p) => new(A * p.X + B * p.Y + C, D * p.X + E * p.Y + F);

    public PageRect Apply(PageRect r) => PageRect.FromPoints([
        Apply(new PagePoint(r.X0, r.Y0)), Apply(new PagePoint(r.X1, r.Y0)),
        Apply(new PagePoint(r.X1, r.Y1)), Apply(new PagePoint(r.X0, r.Y1))]);

    public AlignmentTransform Inverse()
    {
        var det = A * E - B * D;
        if (Math.Abs(det) < 1e-12) throw new InvalidOperationException("Alignment transform is not invertible.");
        double ia = E / det, ib = -B / det, id = -D / det, ie = A / det;
        return new AlignmentTransform(ia, ib, -(ia * C + ib * F), id, ie, -(id * C + ie * F));
    }

    /// <summary>평균 배율 (1 = 같은 축척).</summary>
    public double Scale => Math.Sqrt(Math.Abs(A * E - B * D));

    /// <summary>회전각 (도).</summary>
    public double RotationDegrees => Math.Atan2(D, A) * 180 / Math.PI;

    /// <summary>
    /// 수동 2점 정렬: 이전 도면의 두 점(oldA, oldB)이 개정 도면의 두 점(newA, newB)에 맞도록
    /// 이동·회전·균일 배율을 구한다 (기획서 9장).
    /// </summary>
    public static AlignmentTransform FromTwoPoints(PagePoint oldA, PagePoint oldB, PagePoint newA, PagePoint newB)
    {
        double ox = oldB.X - oldA.X, oy = oldB.Y - oldA.Y;
        double nx = newB.X - newA.X, ny = newB.Y - newA.Y;
        var len2 = ox * ox + oy * oy;
        if (len2 < 1e-9) throw new ArgumentException("The two reference points must be different.");
        // 복소수 나눗셈: (nx + i·ny) / (ox + i·oy) = s·e^{iθ}
        var a = (nx * ox + ny * oy) / len2;
        var b = (ny * ox - nx * oy) / len2;
        return new AlignmentTransform(a, -b, newA.X - (a * oldA.X - b * oldA.Y), b, a, newA.Y - (b * oldA.X + a * oldA.Y));
    }
}

public enum AlignmentMethod
{
    /// <summary>변환 없음 (같은 크기, 같은 위치).</summary>
    Identity,
    /// <summary>자동: 특징점 매칭 + 미세 조정.</summary>
    Automatic,
    /// <summary>사용자가 지정한 2점.</summary>
    Manual,
}

/// <summary>정렬 결과. Quality는 0~1 (겹친 도면 선의 상관도). 낮으면 수동 정렬을 권한다.</summary>
public sealed record AlignmentResult(AlignmentTransform Transform, AlignmentMethod Method, double Quality, string Note)
{
    public const double GoodQuality = 0.6;
    public bool IsReliable => Quality >= GoodQuality;
}

public enum ChangeKind
{
    /// <summary>개정 도면에만 있는 그림 (추가).</summary>
    Added,
    /// <summary>이전 도면에만 있는 그림 (삭제).</summary>
    Removed,
    /// <summary>같은 자리에서 추가와 삭제가 함께 있음 (변경·이동).</summary>
    Modified,
}

public enum ChangeConfidence
{
    High,
    /// <summary>작거나 정렬이 불확실해 사람이 확인해야 하는 후보. 숨기지 않는다 (기획서 8장).</summary>
    Low,
}

public enum ChangeStatus
{
    Pending,
    Accepted,
    Dismissed,
}

/// <summary>변경 후보 영역 하나. 좌표는 개정 도면 페이지 기준(pt).</summary>
public sealed record ChangeRegion(
    int Number,
    ChangeKind Kind,
    PageRect Bounds,
    PageRect BoundsInOld,
    int AddedPixels,
    int RemovedPixels,
    ChangeConfidence Confidence)
{
    public ChangeStatus Status { get; init; } = ChangeStatus.Pending;

    /// <summary>변경 설명 (CAD 비교: "Dimension \"6000\" → \"6500\"" 등). PDF 픽셀 비교는 비어 있다.</summary>
    public string Description { get; init; } = "";
}

/// <summary>비교 설정. 결과와 함께 저장해 같은 조건으로 다시 비교할 수 있게 한다 (재현성 — 기획서 8장).</summary>
public sealed record CompareSettings
{
    /// <summary>비교 해상도 (dpi). 높을수록 정밀하지만 느리다.</summary>
    public double Dpi { get; init; } = 150;

    /// <summary>이 밝기(0~255)보다 어두운 픽셀을 "그림"으로 본다.</summary>
    public int InkThreshold { get; init; } = 200;

    /// <summary>밝기가 이만큼(0~255) 이상 더 어두워야 바뀐 그림으로 본다. 낮을수록 민감하다.</summary>
    public int ContrastDelta { get; init; } = 70;

    /// <summary>위치 허용 오차 (픽셀). 렌더링 미세 차이를 무시한다.</summary>
    public int TolerancePixels { get; init; } = 2;

    /// <summary>이 거리(pt) 안에 있는 차이는 한 변경 영역으로 묶는다.</summary>
    public double GroupDistancePt { get; init; } = 10;

    /// <summary>이 픽셀 수 미만의 점은 잡음으로 버린다 (렌더링 먼지 수준).</summary>
    public int NoisePixels { get; init; } = 3;

    /// <summary>이 픽셀 수 미만의 영역은 "확인 필요(Low)"로 표시한다. 버리지 않는다.</summary>
    public int LowConfidencePixels { get; init; } = 25;

    /// <summary>비교에서 뺄 영역 (개정 도면 좌표, 예: 제목란·날짜).</summary>
    public IReadOnlyList<PageRect> IgnoreRegions { get; init; } = [];

    /// <summary>높은 민감도: 허용 오차·잡음 기준을 낮춘다.</summary>
    public static CompareSettings HighSensitivity => new() { TolerancePixels = 1, NoisePixels = 1, InkThreshold = 230, ContrastDelta = 40, Dpi = 200 };
}

/// <summary>한 쌍의 페이지 비교 결과.</summary>
public sealed record CompareResult(
    IReadOnlyList<ChangeRegion> Regions,
    AlignmentResult Alignment,
    CompareSettings Settings,
    double NewPageWidth,
    double NewPageHeight,
    TimeSpan Duration,
    IReadOnlyList<string> Warnings);
