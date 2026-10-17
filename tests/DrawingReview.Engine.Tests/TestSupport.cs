using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;

namespace DrawingReview.Engine.Tests;

/// <summary>가상 도면을 한 번만 만들어 여러 테스트가 공유한다.</summary>
public sealed class PlanSetFixture
{
    public byte[] PlanSet { get; } = VirtualDrawings.CreatePlanSet();
}

/// <summary>테스트마다 독립된 임시 폴더. 테스트가 끝나면 지운다.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "drtest-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

public static class TestHelpers
{
    public static readonly PdfiumEngine PdfEngine = new();

    /// <summary>저장 후 다시 열기: 실제 파일 왕복을 거친 문서를 돌려준다.</summary>
    public static IPdfDocument SaveAndReopen(IPdfDocument doc, TempDir dir, string name = "roundtrip.pdf")
    {
        var path = dir.File(name);
        doc.SaveTo(path);
        return PdfEngine.Open(path);
    }

    /// <summary>실제 영역이 기대 영역을 덮고, 기대 영역에서 tolerance 이상 벗어나지 않는지 확인.</summary>
    public static void AssertBoundsNear(PageRect expected, PageRect actual, double tolerance)
    {
        Assert.True(actual.Inflate(1).Contains(expected.Inflate(-tolerance / 4)),
            $"Bounds {actual} should cover {expected}");
        Assert.True(expected.Inflate(tolerance).Contains(actual),
            $"Bounds {actual} should stay within {tolerance}pt of {expected}");
    }
}
