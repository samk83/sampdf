using CommunityToolkit.Mvvm.ComponentModel;
using DrawingReview.App.Core.Viewing;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.Comparison;

public enum CompareViewMode
{
    /// <summary>두 도면을 색으로 겹쳐 보기 (이전 빨강, 개정 파랑).</summary>
    Overlay,
    /// <summary>나란히 보기 (확대·이동 동기화 가능).</summary>
    SideBySide,
    /// <summary>바뀐 선만 보기.</summary>
    DifferenceOnly,
}

public enum CloudShape
{
    Rectangle,
    Cloud,
}

/// <summary>Revision Cloud 생성 설정 (기획서 6장).</summary>
public sealed record CloudOptions
{
    public CloudShape Shape { get; init; } = CloudShape.Cloud;
    /// <summary>변경 영역 바깥 여백 (pt).</summary>
    public double MarginPt { get; init; } = 8;
    /// <summary>구름 볼록함 1(작게) 또는 2(크게).</summary>
    public int ArcSize { get; init; } = 2;
    public RgbColor Color { get; init; } = RgbColor.Red;
    public double LineWidth { get; init; } = 2;
    /// <summary>구름 옆에 변경 번호 글상자를 단다.</summary>
    public bool Numbering { get; init; } = true;
}

/// <summary>이전·개정 페이지 짝 (페이지 번호가 달라도 수동으로 지정 — 기획서 9장).</summary>
public sealed record PagePair(int OldPage, int NewPage)
{
    public override string ToString() => $"Old p.{OldPage + 1} ↔ New p.{NewPage + 1}";
}

/// <summary>변경 목록의 한 줄.</summary>
public sealed partial class ChangeItemViewModel(ChangeRegion region) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Number), nameof(Kind), nameof(Confidence), nameof(Bounds), nameof(BoundsInOld), nameof(Summary), nameof(Location))]
    private ChangeRegion _region = region;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private ChangeStatus _status = region.Status;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>이 변경으로 만든 Revision Cloud의 마크업 ID (없으면 null).</summary>
    [ObservableProperty]
    private string? _cloudMarkupId;

    public int Number => Region.Number;
    public ChangeKind Kind => Region.Kind;
    public ChangeConfidence Confidence => Region.Confidence;
    public PageRect Bounds => Region.Bounds;
    public PageRect BoundsInOld => Region.BoundsInOld;

    public string Location => $"x {Bounds.X0:0}, y {Bounds.Y0:0} pt";

    public string Summary => $"{Kind}{(Confidence == ChangeConfidence.Low ? " · check" : "")}";
}

/// <summary>
/// 나란히 보기 동기화: 개정 도면 화면의 확대·위치에 맞춰 이전 도면 화면을 정렬 변환만큼 옮긴다.
/// 회전은 작다고 보고 배율과 이동만 맞춘다.
/// </summary>
public static class ViewportSync
{
    public static void Follow(Viewport source, Viewport target, AlignmentTransform oldToNew)
    {
        // 화면 = newOffset + newZoom × (s × p_old + c)  →  oldZoom = newZoom × s,  oldOffset = newOffset + newZoom × c
        var s = oldToNew.Scale;
        var targetZoom = source.Zoom * s;
        var center = new ScreenPoint(target.View.Width / 2, target.View.Height / 2);
        target.ZoomAt(center, targetZoom / target.Zoom);
        var desiredOffsetX = source.OffsetX + source.Zoom * oldToNew.C;
        var desiredOffsetY = source.OffsetY + source.Zoom * oldToNew.F;
        target.PanBy(desiredOffsetX - target.OffsetX, desiredOffsetY - target.OffsetY);
    }
}
