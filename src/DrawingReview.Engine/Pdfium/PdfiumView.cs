using System.Runtime.InteropServices;
using System.Text;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Pdf;
using static DrawingReview.Engine.Pdfium.PdfiumNative;

namespace DrawingReview.Engine.Pdfium;

/// <summary>
/// PDFium으로 연 문서 하나: 화면 그리기, 글자·검색, 책갈피, 페이지 라벨 등 "읽기" 작업.
/// 바이트 배열을 고정(pin)해서 열므로 파일은 잠그지 않는다. 편집은 <see cref="PdfiumDocument"/>가 PDFsharp 모델로 하고,
/// 바뀐 내용은 새 바이트로 이 뷰를 다시 만들어 반영한다.
/// </summary>
internal sealed class PdfiumView : IDisposable
{
    private readonly GCHandle _pin;
    private IntPtr _doc;

    private PdfiumView(byte[] bytes, GCHandle pin, IntPtr doc)
    {
        Bytes = bytes;
        _pin = pin;
        _doc = doc;
    }

    public byte[] Bytes { get; }

    /// <summary>문서를 연다. 암호가 필요하거나 틀리면 needsPassword = true 와 null. 그 밖의 손상은 예외.</summary>
    public static PdfiumView? Open(byte[] bytes, string? password, out bool needsPassword)
    {
        PdfiumLibrary.EnsureInitialized();
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        lock (PdfiumLibrary.Sync)
        {
            var doc = FPDF_LoadMemDocument(pin.AddrOfPinnedObject(), bytes.Length,
                password == null ? null : Encoding.UTF8.GetBytes(password + "\0"));
            if (doc != IntPtr.Zero)
            {
                needsPassword = false;
                return new PdfiumView(bytes, pin, doc);
            }
            var error = FPDF_GetLastError();
            pin.Free();
            needsPassword = error == FPDF_ERR_PASSWORD;
            if (needsPassword) return null;
            throw new InvalidDataException($"The PDF could not be opened (PDFium error {error}).");
        }
    }

    public int PageCount { get { lock (PdfiumLibrary.Sync) return FPDF_GetPageCount(_doc); } }

    /// <summary>암호를 푼 전체 사본을 만든다 (FPDF_REMOVE_SECURITY). 열 때 쓴 암호의 권한과 무관하게 평문 PDF가 된다.</summary>
    public byte[] SaveDecryptedCopy()
    {
        using var ms = new MemoryStream(Bytes.Length + 1024);
        WriteBlockCallback write = (_, data, size) =>
        {
            // C의 unsigned long: Windows는 32비트, 그 밖은 포인터 크기. 윗자리 쓰레기를 잘라 낸다.
            var n = OperatingSystem.IsWindows() ? (long)((ulong)size & 0xFFFFFFFF) : (long)(ulong)size;
            var chunk = new byte[n];
            Marshal.Copy(data, chunk, 0, (int)n);
            ms.Write(chunk);
            return 1;
        };
        var fw = new FPDF_FILEWRITE { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(write) };
        int ok;
        lock (PdfiumLibrary.Sync) ok = FPDF_SaveAsCopy(_doc, ref fw, FPDF_REMOVE_SECURITY);
        GC.KeepAlive(write);
        if (ok == 0) throw new InvalidDataException("The PDF could not be decrypted for editing.");
        return ms.ToArray();
    }

    /// <summary>암호화 안 된 문서는 -1.</summary>
    public int SecurityRevision { get { lock (PdfiumLibrary.Sync) return FPDF_GetSecurityHandlerRevision(_doc); } }

    public string? PageLabel(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            var label = ReadUtf16((buffer, length) => FPDF_GetPageLabel(_doc, pageIndex, buffer, length));
            return label.Length == 0 ? null : label;
        }
    }

    public int AnnotationCount(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            using var page = LoadPage(pageIndex);
            return FPDFPage_GetAnnotCount(page.Handle);
        }
    }

    public PageGeometry Geometry(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            using var page = LoadPage(pageIndex);
            return GeometryOf(page.Handle);
        }
    }

    private static PageGeometry GeometryOf(IntPtr page)
    {
        // 보이는 영역(CropBox ∩ MediaBox, 비어 있으면 PDFium 기본값)은 PDFium이 그릴 때 쓰는 값과 같은 것을 받는다.
        FPDF_GetPageBoundingBox(page, out var box);
        return new PageGeometry(
            Math.Min(box.Left, box.Right), Math.Min(box.Top, box.Bottom),
            Math.Max(box.Left, box.Right), Math.Max(box.Top, box.Bottom),
            FPDFPage_GetRotation(page) * 90);
    }

    /// <summary>RGB 24비트로 그린다. clip은 보이는 좌표. 결과 원점은 "보이는 좌표 × zoom"을 반올림한 픽셀.</summary>
    public RenderedImage Render(int pageIndex, double zoom, PageRect? clip, bool includeAnnotations)
    {
        lock (PdfiumLibrary.Sync)
        {
            using var page = LoadPage(pageIndex);
            var geometry = GeometryOf(page.Handle);
            var region = clip ?? new PageRect(0, 0, geometry.Width, geometry.Height);
            int originX = (int)Math.Round(region.X0 * zoom), originY = (int)Math.Round(region.Y0 * zoom);
            int width = Math.Max(1, (int)Math.Round(region.X1 * zoom) - originX);
            int height = Math.Max(1, (int)Math.Round(region.Y1 * zoom) - originY);

            var bitmap = FPDFBitmap_CreateEx(width, height, FPDFBitmap_BGR, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero) throw new OutOfMemoryException($"Could not allocate a {width}x{height} render bitmap.");
            try
            {
                FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                // 행렬은 "회전이 반영된 보이는 페이지 좌표(pt)" → 픽셀. 원점을 clip 시작점으로 옮긴다.
                var matrix = new FS_MATRIX { A = (float)zoom, D = (float)zoom, E = -originX, F = -originY };
                var clipping = new FS_RECTF { Left = 0, Top = 0, Right = width, Bottom = height };
                var flags = (includeAnnotations ? FPDF_ANNOT : 0) | FPDF_REVERSE_BYTE_ORDER; // BGR → RGB
                FPDF_RenderPageBitmapWithMatrix(bitmap, page.Handle, ref matrix, ref clipping, flags);

                var stride = FPDFBitmap_GetStride(bitmap);
                var pixels = new byte[stride * height];
                Marshal.Copy(FPDFBitmap_GetBuffer(bitmap), pixels, 0, pixels.Length);
                return new RenderedImage(width, height, stride, pixels, originX, originY, zoom);
            }
            finally { FPDFBitmap_Destroy(bitmap); }
        }
    }

    /// <summary>
    /// 페이지의 단어와 위치(보이는 좌표). 공백·줄바꿈으로 나누고, 글자 상자가 크게 떨어지면 다른 단어로 본다.
    /// 가로 글자뿐 아니라 세로로 선 글자(회전된 치수 문자 등)도 상자가 위아래로 이어지면 한 단어로 묶는다.
    /// </summary>
    public IReadOnlyList<TextWord> Words(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            using var page = LoadPage(pageIndex);
            var geometry = GeometryOf(page.Handle);
            var textPage = FPDFText_LoadPage(page.Handle);
            if (textPage == IntPtr.Zero) return [];
            try
            {
                var words = new List<TextWord>();
                var text = new StringBuilder();
                PageRect bounds = default;
                double lastX0 = 0, lastX1 = 0, lastY0 = 0, lastY1 = 0, lastSize = 0;

                void Flush()
                {
                    if (text.Length > 0) words.Add(new TextWord(text.ToString(), bounds));
                    text.Clear();
                }

                var count = FPDFText_CountChars(textPage);
                for (int i = 0; i < count; i++)
                {
                    var unicode = FPDFText_GetUnicode(textPage, i);
                    if (unicode > 0xFFFF) { Flush(); continue; }
                    var ch = (char)unicode;
                    if (char.IsWhiteSpace(ch) || char.IsControl(ch)) { Flush(); continue; }
                    if (FPDFText_GetCharBox(textPage, i, out var left, out var right, out var bottom, out var top) == 0) { Flush(); continue; }
                    var box = geometry.ToVisibleRect(left, bottom, right, top);
                    var size = FPDFText_GetFontSize(textPage, i);
                    if (text.Length > 0)
                    {
                        // 공백 글자가 없어도 멀리 떨어졌거나 다른 줄이면 단어를 끊는다 (도면 글자는 띄어쓰기 없이 흩어져 있을 수 있다)
                        var tolerance = Math.Max(size, lastSize) * 0.5;
                        var sameLine = box.Y0 < lastY1 && box.Y1 > lastY0;
                        var sameColumn = box.X0 < lastX1 && box.X1 > lastX0;
                        var horizontalGap = box.X0 - lastX1;
                        var verticalGap = Math.Min(Math.Abs(box.Y0 - lastY1), Math.Abs(lastY0 - box.Y1));
                        var joinsRow = sameLine && horizontalGap <= tolerance && box.X1 >= lastX1 - tolerance * 2;
                        var joinsColumn = !sameLine && sameColumn && verticalGap <= tolerance;
                        if (!joinsRow && !joinsColumn) Flush();
                    }
                    bounds = text.Length == 0 ? box : new PageRect(Math.Min(bounds.X0, box.X0), Math.Min(bounds.Y0, box.Y0), Math.Max(bounds.X1, box.X1), Math.Max(bounds.Y1, box.Y1));
                    text.Append(ch);
                    lastX0 = box.X0; lastX1 = box.X1; lastY0 = box.Y0; lastY1 = box.Y1; lastSize = size;
                }
                Flush();
                return words;
            }
            finally { FPDFText_ClosePage(textPage); }
        }
    }

    /// <summary>글자 검색 (대소문자 구분 없음). 일치한 영역들을 보이는 좌표로 돌려준다.</summary>
    public IReadOnlyList<PageRect> Search(int pageIndex, string needle, int maxHits)
    {
        lock (PdfiumLibrary.Sync)
        {
            using var page = LoadPage(pageIndex);
            var geometry = GeometryOf(page.Handle);
            var textPage = FPDFText_LoadPage(page.Handle);
            if (textPage == IntPtr.Zero) return [];
            try
            {
                var hits = new List<PageRect>();
                var handle = FPDFText_FindStart(textPage, needle, 0, 0);
                if (handle == IntPtr.Zero) return hits;
                try
                {
                    while (hits.Count < maxHits && FPDFText_FindNext(handle) != 0)
                    {
                        var start = FPDFText_GetSchResultIndex(handle);
                        var length = FPDFText_GetSchCount(handle);
                        var rects = FPDFText_CountRects(textPage, start, length);
                        PageRect? union = null;
                        for (int r = 0; r < rects; r++)
                        {
                            if (FPDFText_GetRect(textPage, r, out var left, out var top, out var right, out var bottom) == 0) continue;
                            var box = geometry.ToVisibleRect(left, bottom, right, top);
                            union = union is { } u ? new PageRect(Math.Min(u.X0, box.X0), Math.Min(u.Y0, box.Y0), Math.Max(u.X1, box.X1), Math.Max(u.Y1, box.Y1)) : box;
                        }
                        if (union is { } hit) hits.Add(hit);
                    }
                }
                finally { FPDFText_FindClose(handle); }
                return hits;
            }
            finally { FPDFText_ClosePage(textPage); }
        }
    }

    public IReadOnlyList<OutlineItem> Outline()
    {
        lock (PdfiumLibrary.Sync)
        {
            var items = new List<OutlineItem>();
            var visited = new HashSet<IntPtr>();
            Walk(IntPtr.Zero, 1, items, visited);
            return items;
        }
    }

    private void Walk(IntPtr parent, int level, List<OutlineItem> items, HashSet<IntPtr> visited)
    {
        const int maxItems = 100_000;
        for (var bookmark = FPDFBookmark_GetFirstChild(_doc, parent); bookmark != IntPtr.Zero && visited.Add(bookmark) && items.Count < maxItems;
             bookmark = FPDFBookmark_GetNextSibling(_doc, bookmark))
        {
            var title = ReadUtf16((buffer, length) => FPDFBookmark_GetTitle(bookmark, buffer, length));
            var dest = FPDFBookmark_GetDest(_doc, bookmark);
            if (dest == IntPtr.Zero)
            {
                var action = FPDFBookmark_GetAction(bookmark);
                if (action != IntPtr.Zero) dest = FPDFAction_GetDest(_doc, action);
            }
            var pageIndex = dest == IntPtr.Zero ? -1 : FPDFDest_GetDestPageIndex(_doc, dest);
            items.Add(new OutlineItem(level, title, pageIndex));
            Walk(bookmark, level + 1, items, visited);
        }
    }

    private PageHandle LoadPage(int pageIndex)
    {
        var count = FPDF_GetPageCount(_doc);
        if (pageIndex < 0 || pageIndex >= count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"Page index must be 0..{count - 1}.");
        var page = FPDF_LoadPage(_doc, pageIndex);
        if (page == IntPtr.Zero) throw new InvalidDataException($"Page {pageIndex + 1} could not be loaded.");
        return new PageHandle(page);
    }

    private readonly struct PageHandle(IntPtr handle) : IDisposable
    {
        public IntPtr Handle => handle;
        public void Dispose() => FPDF_ClosePage(handle);
    }

    /// <summary>Dispose를 잊어도 네이티브 문서와 고정한 바이트 배열이 영원히 남지 않도록 한다.</summary>
    ~PdfiumView() => Dispose();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        lock (PdfiumLibrary.Sync)
        {
            if (_doc != IntPtr.Zero) { FPDF_CloseDocument(_doc); _doc = IntPtr.Zero; }
        }
        if (_pin.IsAllocated) _pin.Free();
    }
}
