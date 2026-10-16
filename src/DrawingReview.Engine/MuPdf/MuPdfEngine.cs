using DrawingReview.Engine.Documents;

namespace DrawingReview.Engine.MuPdf;

/// <summary>MuPDF.NET(AGPL) 기반 엔진. 개인 로컬 사용 전제로 채택 검증 중.</summary>
public sealed class MuPdfEngine : IPdfEngine
{
    /// <summary>Windows에서 한글이 깨지지 않도록 문자열 보정을 먼저 적용한다 (MuPdfUtf8Fix 참고).</summary>
    static MuPdfEngine() => MuPdfUtf8Fix.Ensure();

    public string Name => "MuPDF.NET";

    /// <summary>PDF 대신 열 수 있는 그림 파일 확장자. 열 때 한 페이지짜리 PDF로 바꾼다.</summary>
    public static readonly IReadOnlySet<string> ImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp" };

    public static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public IPdfDocument Open(string path)
    {
        var fullPath = Path.GetFullPath(path);
        // 파일을 메모리로 읽어 연다: 원본 파일 핸들을 잡고 있지 않아야 같은 경로로 안전 저장(교체)이 가능하다.
        var bytes = File.ReadAllBytes(fullPath);
        if (IsImageFile(fullPath))
            // 그림은 PDF로 바꿔 연다 (마크업·저장이 PDF와 같게 동작). 원래 그림 경로에 PDF를 덮어쓰지 않도록 원본 경로는 비워 둔다.
            return OpenImage(bytes, Path.GetExtension(fullPath).TrimStart('.'));
        return new MuPdfDocument(bytes, fullPath);
    }

    /// <summary>그림(PNG·JPG·TIFF·BMP) 바이트를 PDF로 바꿔 연다. 여러 장 TIFF는 여러 페이지가 된다.</summary>
    public IPdfDocument OpenImage(byte[] imageBytes, string fileType)
    {
        using var image = new MuPDF.NET.Document(stream: imageBytes, fileType: fileType.ToLowerInvariant());
        if (image.PageCount == 0) throw new InvalidDataException("The image could not be read.");
        return new MuPdfDocument(image.Convert2Pdf(), sourcePath: null);
    }

    public void WriteVectorPdf(string path, IReadOnlyList<Vector.VectorPage> pages) => MuPdfVectorWriter.Write(Path.GetFullPath(path), pages);

    /// <summary>테스트·샘플 생성용: 메모리의 PDF 바이트로 연다.</summary>
    public IPdfDocument Open(byte[] pdfBytes) => new MuPdfDocument(pdfBytes, sourcePath: null);
}
