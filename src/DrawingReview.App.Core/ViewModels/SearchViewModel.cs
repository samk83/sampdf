using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>캔버스에 강조할 검색 결과 (페이지와 영역들).</summary>
public sealed record SearchHit(int PageIndex, IReadOnlyList<PageRect> Rects);

/// <summary>검색 결과 한 줄.</summary>
public sealed record SearchResult(int PageIndex, string PageCaption, PageRect Rect, string Context);

/// <summary>왼쪽 Search 패널: 문서 전체에서 글자를 찾고, 결과를 고르면 그 위치로 이동해 강조한다.</summary>
public sealed partial class SearchViewModel : ObservableObject
{
    private readonly Func<DocumentViewModel?> _document;
    private CancellationTokenSource? _cts;

    public SearchViewModel(Func<DocumentViewModel?> document) => _document = document;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private SearchResult? _selectedResult;
    [ObservableProperty] private SearchHit? _currentHit;

    public ObservableCollection<SearchResult> Results { get; } = [];

    [RelayCommand]
    public async Task SearchAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        Results.Clear();
        CurrentHit = null;
        var doc = _document();
        var q = Query.Trim();
        if (doc == null || q.Length == 0) { Summary = ""; return; }
        IsSearching = true;
        try
        {
            for (int i = 0; i < doc.Pages.Count; i++)
            {
                var page = i;
                var (hits, words) = await doc.Gate.RunAsync(() => (doc.Document.SearchText(page, q), doc.Document.GetWords(page)), cts.Token);
                if (cts.IsCancellationRequested) return;
                foreach (var h in hits)
                    Results.Add(new SearchResult(page, doc.Pages[page].Caption, h, ContextLine(words, h)));
            }
            var pages = Results.Select(r => r.PageIndex).Distinct().Count();
            Summary = Results.Count == 0 ? "No results" : $"{Results.Count} results on {pages} page{(pages == 1 ? "" : "s")}";
        }
        catch (OperationCanceledException) { }
        finally { if (_cts == cts) IsSearching = false; }
    }

    /// <summary>검색어가 있는 줄의 단어들을 이어 붙여 보여준다.</summary>
    private static string ContextLine(IReadOnlyList<TextWord> words, PageRect hit)
    {
        var line = words.Where(w => w.Bounds.Y0 < hit.Y1 && w.Bounds.Y1 > hit.Y0 && Math.Abs(w.Bounds.Center.X - hit.Center.X) < 400)
            .OrderBy(w => w.Bounds.X0).Select(w => w.Text);
        var text = string.Join(" ", line);
        return text.Length > 80 ? text[..77] + "…" : text;
    }

    partial void OnSelectedResultChanged(SearchResult? value)
    {
        if (value == null || _document() is not { } doc) return;
        doc.GoToPage(value.PageIndex);
        var r = value.Rect;
        if (Math.Max(r.Width, r.Height) * doc.Viewport.Zoom < 12)
            doc.Viewport.ZoomToRegion(r.Inflate(Math.Max(r.Width, r.Height) * 4), maxZoom: Viewing.Viewport.ActualSizeZoom * 2);
        else
            doc.Viewport.CenterOn(r.Center);
        CurrentHit = new SearchHit(value.PageIndex, Results.Where(x => x.PageIndex == value.PageIndex).Select(x => x.Rect).ToList());
    }

    [RelayCommand]
    private void Next() => Step(+1);

    [RelayCommand]
    private void Previous() => Step(-1);

    private void Step(int delta)
    {
        if (Results.Count == 0) return;
        var i = SelectedResult == null ? (delta > 0 ? 0 : Results.Count - 1) : (Results.IndexOf(SelectedResult) + delta + Results.Count) % Results.Count;
        SelectedResult = Results[i];
    }

    [RelayCommand]
    private void Clear()
    {
        _cts?.Cancel();
        Query = "";
        Results.Clear();
        Summary = "";
        CurrentHit = null;
    }
}

/// <summary>왼쪽 Bookmarks 패널 (기존 책갈피 사용 — P0).</summary>
public sealed partial class BookmarksViewModel : ObservableObject
{
    private readonly Func<DocumentViewModel?> _document;

    public BookmarksViewModel(Func<DocumentViewModel?> document) => _document = document;

    public ObservableCollection<OutlineItem> Items { get; } = [];

    [ObservableProperty] private OutlineItem? _selected;

    public bool IsEmpty => Items.Count == 0;

    public async Task LoadAsync()
    {
        var doc = _document();
        var items = doc == null ? [] : await doc.Gate.RunAsync(() => doc.Document.GetOutline());
        if (_document() != doc) return; // 그사이 다른 탭으로 바뀜
        // 다 읽은 뒤 한 번에 바꾼다 (탭 전환이 겹쳐도 중복되지 않게)
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSelectedChanged(OutlineItem? value)
    {
        if (value is { PageIndex: >= 0 } && _document() is { } doc)
        {
            doc.GoToPage(value.PageIndex);
            doc.Viewport.FitPage();
        }
    }
}
