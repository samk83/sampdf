using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Viewing;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>
/// 탭 하나 = 열린 PDF 하나. 페이지 이동, 확대·이동 상태, 렌더링 요청을 담당한다.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject, IDisposable
{
    /// <summary>미리보기(저해상도 전체 페이지)의 긴 변 픽셀 수. 확대 중 고해상도가 오기 전에 보여준다.</summary>
    public const int PreviewLongEdge = 2000;
    public const int ThumbnailLongEdge = 180;
    private const int PreviewCacheSize = 6;

    private readonly IPdfDocument _document;
    private readonly EngineGate _gate = new();
    private readonly LinkedList<(int Page, RenderedImage Image)> _previewCache = new();
    private bool _disposed;

    public DocumentViewModel(IPdfDocument document, string filePath)
    {
        _document = document;
        FilePath = filePath;
        for (int i = 0; i < document.PageCount; i++)
            Pages.Add(new PageThumbnailViewModel(document.GetPageInfo(i)));
        Viewport.Changed += (_, _) => OnPropertyChanged(nameof(ZoomText));
        if (Pages.Count > 0)
        {
            Pages[0].IsCurrent = true;
            Viewport.SetPage(Pages[0].Info.Width, Pages[0].Info.Height);
        }
    }

    public string FilePath { get; private set; }

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>다른 이름으로 저장한 뒤 이 탭이 새 파일을 가리키게 한다.</summary>
    public void RenameTo(string newPath)
    {
        FilePath = Path.GetFullPath(newPath);
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>탭 제목. 저장하지 않은 변경이 있으면 표시한다.</summary>
    public string Title => IsModified ? FileName + " *" : FileName;

    public ObservableCollection<PageThumbnailViewModel> Pages { get; } = [];

    public Viewport Viewport { get; } = new();

    public DocumentSecurityInfo Security => _document.Security;

    /// <summary>엔진 직접 접근이 필요한 기능(마크업 편집 등)이 같은 순서 보장 아래 실행되도록 공유한다.</summary>
    internal EngineGate Gate => _gate;

    internal IPdfDocument Document => _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isModified;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage), nameof(PageStatusText))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand), nameof(FirstPageCommand), nameof(LastPageCommand))]
    private int _currentPageIndex;

    public PageThumbnailViewModel? CurrentPage => CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count ? Pages[CurrentPageIndex] : null;

    public string PageStatusText => CurrentPage is { } p
        ? $"Page {p.Index + 1} of {Pages.Count}" + (p.Info.Label is { } l ? $" ({l})" : "")
        : "No page";

    public string PageSizeText => CurrentPage is { } p ? $"{p.Info.Width:0} × {p.Info.Height:0} pt" : "";

    public string ZoomText => $"{Viewport.ZoomPercent:0}%";

    /// <summary>페이지가 바뀌면 화면(PageView)이 새 페이지를 그리도록 알린다.</summary>
    public event EventHandler? PageChanged;

    partial void OnCurrentPageIndexChanged(int oldValue, int newValue)
    {
        if (oldValue >= 0 && oldValue < Pages.Count) Pages[oldValue].IsCurrent = false;
        if (CurrentPage is { } p)
        {
            p.IsCurrent = true;
            Viewport.SetPage(p.Info.Width, p.Info.Height);
        }
        OnPropertyChanged(nameof(PageSizeText));
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void GoToPage(int index)
    {
        if (index >= 0 && index < Pages.Count) CurrentPageIndex = index;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage() => GoToPage(CurrentPageIndex + 1);

    private bool CanGoNext() => CurrentPageIndex < Pages.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage() => GoToPage(CurrentPageIndex - 1);

    private bool CanGoPrevious() => CurrentPageIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void FirstPage() => GoToPage(0);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void LastPage() => GoToPage(Pages.Count - 1);

    [RelayCommand]
    private void FitPage() => Viewport.FitPage();

    [RelayCommand]
    private void FitWidth() => Viewport.FitWidth();

    [RelayCommand]
    private void ActualSize() => Viewport.SetZoom(Viewport.ActualSizeZoom);

    [RelayCommand]
    private void ZoomIn() => Viewport.SetZoom(Viewport.Zoom * 1.25);

    [RelayCommand]
    private void ZoomOut() => Viewport.SetZoom(Viewport.Zoom / 1.25);

    /// <summary>현재 페이지 전체를 저해상도로 렌더링 (캐시). 확대·이동 중 즉시 보여줄 바탕 그림.</summary>
    public async Task<RenderedImage> RenderPreviewAsync(int pageIndex, CancellationToken ct = default)
    {
        lock (_previewCache)
        {
            var hit = _previewCache.FirstOrDefault(e => e.Page == pageIndex);
            if (hit.Image != null) return hit.Image;
        }
        var info = Pages[pageIndex].Info;
        var zoom = PreviewLongEdge / Math.Max(info.Width, info.Height);
        var image = await _gate.RunAsync(() => _document.RenderPage(pageIndex, zoom), ct).ConfigureAwait(false);
        lock (_previewCache)
        {
            _previewCache.AddFirst((pageIndex, image));
            while (_previewCache.Count > PreviewCacheSize) _previewCache.RemoveLast();
        }
        return image;
    }

    /// <summary>보이는 영역만 화면 해상도로 렌더링. pixelsPerPoint = 화면 확대율 × 모니터 배율.</summary>
    public Task<RenderedImage> RenderRegionAsync(int pageIndex, PageRect region, double pixelsPerPoint, CancellationToken ct = default) =>
        _gate.RunAsync(() => _document.RenderPage(pageIndex, pixelsPerPoint, region), ct);

    /// <summary>썸네일을 앞 페이지부터 차례로 만든다.</summary>
    public async Task LoadThumbnailsAsync(CancellationToken ct = default)
    {
        foreach (var page in Pages)
        {
            if (ct.IsCancellationRequested || _disposed) return;
            if (page.Image != null) continue;
            var zoom = ThumbnailLongEdge / Math.Max(page.Info.Width, page.Info.Height);
            page.Image = await _gate.RunAsync(() => _document.RenderPage(page.Index, zoom), ct).ConfigureAwait(true);
        }
    }

    /// <summary>문서 내용이 바뀐 뒤 렌더링 캐시를 비운다 (마크업 추가·삭제 후).</summary>
    public void InvalidateRenderCache(int? pageIndex = null)
    {
        lock (_previewCache)
        {
            if (pageIndex is { } p)
            {
                var node = _previewCache.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (node.Value.Page == p) _previewCache.Remove(node);
                    node = next;
                }
            }
            else _previewCache.Clear();
        }
        RenderInvalidated?.Invoke(this, pageIndex);
    }

    /// <summary>캐시가 무효화되어 화면이 다시 그려야 할 때. 인자는 페이지 번호(null = 전체).</summary>
    public event EventHandler<int?>? RenderInvalidated;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // 진행 중인 엔진 작업이 끝난 뒤 닫는다.
        _ = _gate.RunAsync(() => _document.Dispose()).ContinueWith(_ => _gate.Dispose(), TaskScheduler.Default);
    }
}
