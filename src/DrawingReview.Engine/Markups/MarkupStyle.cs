namespace DrawingReview.Engine.Markups;

/// <summary>0~1 범위의 RGB 색상.</summary>
public readonly record struct RgbColor(double R, double G, double B)
{
    public static readonly RgbColor Red = new(1, 0, 0);
    public static readonly RgbColor Blue = new(0, 0, 1);
    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor Yellow = new(1, 1, 0);

    public float[] ToArray() => [(float)R, (float)G, (float)B];
}

public sealed record MarkupStyle
{
    public RgbColor StrokeColor { get; init; } = RgbColor.Red;
    public RgbColor? FillColor { get; init; }
    public double LineWidth { get; init; } = 1.5;
    public double Opacity { get; init; } = 1.0;
    public double FontSize { get; init; } = 12;
    public RgbColor TextColor { get; init; } = RgbColor.Red;

    public static readonly MarkupStyle Default = new();

    /// <summary>영역 하이라이트 기본값: 노란색 40% (Multiply 혼합으로 도면 선이 비쳐 보인다).</summary>
    public static readonly MarkupStyle Highlighter = new() { StrokeColor = RgbColor.Yellow, Opacity = 0.4, LineWidth = 0 };
}
