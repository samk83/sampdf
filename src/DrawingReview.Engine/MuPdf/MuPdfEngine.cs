using DrawingReview.Engine.Documents;

namespace DrawingReview.Engine.MuPdf;

/// <summary>MuPDF.NET(AGPL) 기반 엔진. 개인 로컬 사용 전제로 채택 검증 중.</summary>
public sealed class MuPdfEngine : IPdfEngine
{
    public string Name => "MuPDF.NET";

    public IPdfDocument Open(string path)
    {
        var fullPath = Path.GetFullPath(path);
        // 파일을 메모리로 읽어 연다: 원본 파일 핸들을 잡고 있지 않아야 같은 경로로 안전 저장(교체)이 가능하다.
        var bytes = File.ReadAllBytes(fullPath);
        return new MuPdfDocument(bytes, fullPath);
    }

    public void WriteVectorPdf(string path, IReadOnlyList<Vector.VectorPage> pages) => MuPdfVectorWriter.Write(Path.GetFullPath(path), pages);

    /// <summary>테스트·샘플 생성용: 메모리의 PDF 바이트로 연다.</summary>
    public IPdfDocument Open(byte[] pdfBytes) => new MuPdfDocument(pdfBytes, sourcePath: null);
}
