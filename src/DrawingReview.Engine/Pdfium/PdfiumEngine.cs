using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Pdf;

namespace DrawingReview.Engine.Pdfium;

/// <summary>
/// 기본 PDF 엔진: 화면 그리기·글자·검색은 PDFium(BSD), 주석 쓰기·저장·평탄화는 PDFsharp(MIT) + 직접 만든 외형.
/// 둘 다 무료·비공개 사용에 제약이 없는 부품이다 (docs/THIRD_PARTY.md).
/// </summary>
public sealed class PdfiumEngine : IPdfEngine
{
    public string Name => "PDFium + PDFsharp";

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
        return new PdfiumDocument(bytes, fullPath);
    }

    /// <summary>그림(PNG·JPG·TIFF·BMP) 바이트를 PDF로 바꿔 연다. 여러 장 TIFF는 여러 페이지가 된다.</summary>
    public IPdfDocument OpenImage(byte[] imageBytes, string fileType) =>
        new PdfiumDocument(ImagePdfBuilder.Build(imageBytes, fileType), sourcePath: null);

    public void WriteVectorPdf(string path, IReadOnlyList<Vector.VectorPage> pages) => VectorPdfWriter.Write(Path.GetFullPath(path), pages);

    /// <summary>테스트·샘플 생성용: 메모리의 PDF 바이트로 연다.</summary>
    public IPdfDocument Open(byte[] pdfBytes) => new PdfiumDocument(pdfBytes, sourcePath: null);
}
