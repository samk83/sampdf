using System.Runtime.InteropServices;

namespace DrawingReview.Engine.Pdfium;

/// <summary>
/// PDFium(크롬의 PDF 엔진, BSD 라이선스) 직접 호출. 본체는 bblanchon.PDFium.* 패키지가 runtimes/ 폴더에 넣는
/// pdfium.dll / libpdfium.so 이고, .NET이 실행 환경에 맞는 파일을 고른다.
///
/// 이 앱이 쓰는 함수만 선언한다 (그리기, 글자·검색, 책갈피, 페이지 라벨, 암호 확인·해제 사본).
/// PDFium은 스레드 안전하지 않으므로 모든 호출은 <see cref="PdfiumLibrary.Sync"/> 잠금 안에서 한다.
/// </summary>
internal static class PdfiumNative
{
    private const string Lib = "pdfium";

    public const int FPDF_ANNOT = 0x01;
    public const int FPDF_REVERSE_BYTE_ORDER = 0x10;
    public const int FPDFBitmap_BGR = 2;
    public const uint FPDF_ERR_PASSWORD = 4;
    public const int FPDF_REMOVE_SECURITY = 3; // FPDF_SaveAsCopy: 암호를 풀어 저장

    /// <summary>FPDF_SaveAsCopy가 쓴 조각을 받는 콜백 (FPDF_FILEWRITE.WriteBlock). 성공이면 1.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int WriteBlockCallback(IntPtr self, IntPtr data, UIntPtr size);

    /// <summary>FPDF_FILEWRITE: version(=1) + WriteBlock 함수 포인터.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FPDF_FILEWRITE { public int Version; public IntPtr WriteBlock; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FS_RECTF { public float Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FS_MATRIX { public float A, B, C, D, E, F; }

    [DllImport(Lib)] public static extern void FPDF_InitLibrary();
    [DllImport(Lib)] public static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, byte[]? password);
    [DllImport(Lib)] public static extern void FPDF_CloseDocument(IntPtr document);
    [DllImport(Lib)] public static extern uint FPDF_GetLastError();
    [DllImport(Lib)] public static extern int FPDF_GetPageCount(IntPtr document);
    [DllImport(Lib)] public static extern int FPDF_GetSecurityHandlerRevision(IntPtr document);
    [DllImport(Lib)] public static extern int FPDF_SaveAsCopy(IntPtr document, ref FPDF_FILEWRITE fileWrite, uint flags);
    [DllImport(Lib)] public static extern uint FPDF_GetPageLabel(IntPtr document, int pageIndex, IntPtr buffer, uint bufferLength);

    [DllImport(Lib)] public static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);
    [DllImport(Lib)] public static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Lib)] public static extern int FPDFPage_GetRotation(IntPtr page);
    [DllImport(Lib)] public static extern int FPDF_GetPageBoundingBox(IntPtr page, out FS_RECTF rect);
    [DllImport(Lib)] public static extern int FPDFPage_GetAnnotCount(IntPtr page);

    [DllImport(Lib)] public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);
    [DllImport(Lib)] public static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    [DllImport(Lib)] public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
    [DllImport(Lib)] public static extern int FPDFBitmap_GetStride(IntPtr bitmap);
    [DllImport(Lib)] public static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Lib)] public static extern void FPDF_RenderPageBitmapWithMatrix(IntPtr bitmap, IntPtr page, ref FS_MATRIX matrix, ref FS_RECTF clipping, int flags);

    [DllImport(Lib)] public static extern IntPtr FPDFText_LoadPage(IntPtr page);
    [DllImport(Lib)] public static extern void FPDFText_ClosePage(IntPtr textPage);
    [DllImport(Lib)] public static extern int FPDFText_CountChars(IntPtr textPage);
    [DllImport(Lib)] public static extern uint FPDFText_GetUnicode(IntPtr textPage, int index);
    [DllImport(Lib)] public static extern int FPDFText_GetCharBox(IntPtr textPage, int index, out double left, out double right, out double bottom, out double top);
    [DllImport(Lib)] public static extern double FPDFText_GetFontSize(IntPtr textPage, int index);
    [DllImport(Lib, CharSet = CharSet.Unicode)]
    public static extern IntPtr FPDFText_FindStart(IntPtr textPage, [MarshalAs(UnmanagedType.LPWStr)] string findWhat, uint flags, int startIndex);
    [DllImport(Lib)] public static extern int FPDFText_FindNext(IntPtr handle);
    [DllImport(Lib)] public static extern int FPDFText_GetSchResultIndex(IntPtr handle);
    [DllImport(Lib)] public static extern int FPDFText_GetSchCount(IntPtr handle);
    [DllImport(Lib)] public static extern void FPDFText_FindClose(IntPtr handle);
    [DllImport(Lib)] public static extern int FPDFText_CountRects(IntPtr textPage, int startIndex, int count);
    [DllImport(Lib)] public static extern int FPDFText_GetRect(IntPtr textPage, int rectIndex, out double left, out double top, out double right, out double bottom);

    [DllImport(Lib)] public static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr document, IntPtr bookmark);
    [DllImport(Lib)] public static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr document, IntPtr bookmark);
    [DllImport(Lib)] public static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, IntPtr buffer, uint bufferLength);
    [DllImport(Lib)] public static extern IntPtr FPDFBookmark_GetDest(IntPtr document, IntPtr bookmark);
    [DllImport(Lib)] public static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);
    [DllImport(Lib)] public static extern IntPtr FPDFAction_GetDest(IntPtr document, IntPtr action);
    [DllImport(Lib)] public static extern int FPDFDest_GetDestPageIndex(IntPtr document, IntPtr dest);

    /// <summary>"버퍼 크기를 물은 뒤 채우는" PDFium 문자열 함수(UTF-16, 끝에 NUL)를 .NET 문자열로 바꾼다.</summary>
    public static string ReadUtf16(Func<IntPtr, uint, uint> read)
    {
        var length = read(IntPtr.Zero, 0);
        if (length <= 2) return "";
        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            read(buffer, length);
            return Marshal.PtrToStringUni(buffer, (int)length / 2 - 1) ?? "";
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}

/// <summary>PDFium 초기화(한 번)와 전역 잠금.</summary>
internal static class PdfiumLibrary
{
    /// <summary>PDFium은 문서가 달라도 동시에 부르면 안 된다. 모든 네이티브 호출은 이 잠금 안에서 한다.</summary>
    public static readonly object Sync = new();

    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized) return;
            PdfiumNative.FPDF_InitLibrary();
            _initialized = true;
        }
    }
}
