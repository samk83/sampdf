namespace DrawingReview.Engine.Geometry;

/// <summary>
/// 렌더링 결과 (RGB 24비트, 한 줄 = Stride 바이트).
/// OriginX/OriginY는 "페이지 좌표 × zoom" 기준 픽셀 위치로, 잘라낸(clip) 렌더링을 화면에 정확히 겹치는 데 쓴다.
/// </summary>
public sealed record RenderedImage(int Width, int Height, int Stride, byte[] Pixels, int OriginX, int OriginY, double Zoom)
{
    /// <summary>이 이미지가 덮는 영역 (보이는 페이지 좌표).</summary>
    public PageRect Region => new(OriginX / Zoom, OriginY / Zoom, (OriginX + Width) / Zoom, (OriginY + Height) / Zoom);
}
