using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Services;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

public enum ReviewSortColumn
{
    Page, Type, Subject, Comment, Author, Status, Modified,
}

/// <summary>
/// 아래쪽 Review List (기획서 8장): 거르기·정렬, 숨겨진 개수 표시, 행 선택 ↔ 캔버스 선택 동기화,
/// 상태 일괄 변경(되돌리기 가능), CSV 보고서.
/// </summary>
public sealed partial class ReviewListViewModel : ObservableObject
{
    public const string AllStatuses = "All";
    public const string NotResolved = "Not Resolved";

    private readonly IDialogService _dialogs;
    private readonly Func<string, string, Task>? _writeFile;
    private DocumentViewModel? _document;
    private bool _syncingSelection;

    /// <param name="writeFile">파일 쓰기 (경로, 내용). 테스트에서 가로챌 수 있게 주입한다. null이면 UTF-8(BOM)으로 쓴다.</param>
    public ReviewListViewModel(IDialogService dialogs, Func<string, string, Task>? writeFile = null)
    {
        _dialogs = dialogs;
        _writeFile = writeFile;
    }

    public ObservableCollection<MarkupItemViewModel> Items { get; } = [];

    /// <summary>화면에서 선택된 행 (DataGrid가 바꾼다).</summary>
    public ObservableCollection<MarkupItemViewModel> SelectedItems { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } = [AllStatuses, NotResolved, .. ReviewStatus.All];

    public IReadOnlyList<string> Statuses => ReviewStatus.All;

    [ObservableProperty]
    private string _statusFilter = AllStatuses;

    [ObservableProperty]
    private string _textFilter = "";

    [ObservableProperty]
    private bool _currentPageOnly;

    [ObservableProperty]
    private ReviewSortColumn _sortColumn = ReviewSortColumn.Page;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText), nameof(HasHidden))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText), nameof(HasHidden))]
    private int _visibleCount;

    public int HiddenCount => TotalCount - VisibleCount;

    public bool HasHidden => HiddenCount > 0;

    /// <summary>"Showing 6 of 8 — 2 hidden by filter" (누락 방지: 기획서 8장).</summary>
    public string SummaryText => HasHidden
        ? $"Showing {VisibleCount} of {TotalCount} — {HiddenCount} hidden by filter"
        : $"{TotalCount} markups";

    /// <summary>행을 골랐을 때 캔버스가 그 마크업으로 이동하도록 알린다.</summary>
    public event EventHandler<MarkupItemViewModel>? NavigateRequested;

    public DocumentViewModel? Document
    {
        get => _document;
        set
        {
            if (_document == value) return;
            if (_document != null)
            {
                _document.MarkupsChanged -= OnMarkupsChanged;
                _document.SelectionChanged -= OnDocumentSelectionChanged;
                _document.PageChanged -= OnPageChanged;
            }
            _document = value;
            if (_document != null)
            {
                _document.MarkupsChanged += OnMarkupsChanged;
                _document.SelectionChanged += OnDocumentSelectionChanged;
                _document.PageChanged += OnPageChanged;
            }
            OnPropertyChanged();
            Refresh();
        }
    }

    partial void OnStatusFilterChanged(string value) => Refresh();
    partial void OnTextFilterChanged(string value) => Refresh();
    partial void OnCurrentPageOnlyChanged(bool value) => Refresh();
    partial void OnSortColumnChanged(ReviewSortColumn value) => Refresh();
    partial void OnSortDescendingChanged(bool value) => Refresh();

    private void OnMarkupsChanged(object? sender, EventArgs e) => Refresh();
    private void OnPageChanged(object? sender, EventArgs e) { if (CurrentPageOnly) Refresh(); }

    /// <summary>열 머리글을 누르면: 같은 열이면 순서를 뒤집고, 다른 열이면 오름차순.</summary>
    [RelayCommand]
    private void SortBy(ReviewSortColumn column)
    {
        if (SortColumn == column) SortDescending = !SortDescending;
        else { SortColumn = column; SortDescending = false; }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        StatusFilter = AllStatuses;
        TextFilter = "";
        CurrentPageOnly = false;
    }

    /// <summary>필터·정렬을 다시 적용한다.</summary>
    public void Refresh()
    {
        var all = _document?.Markups.ToList() ?? [];
        TotalCount = all.Count;
        IEnumerable<MarkupItemViewModel> q = all.Where(Matches);
        q = Sort(q);
        var list = q.ToList();
        Items.Clear();
        foreach (var i in list) Items.Add(i);
        VisibleCount = list.Count;
        SyncSelectionFromDocument();
    }

    private bool Matches(MarkupItemViewModel m)
    {
        if (CurrentPageOnly && _document != null && m.PageIndex != _document.CurrentPageIndex) return false;
        if (StatusFilter == NotResolved && m.Status == ReviewStatus.Resolved) return false;
        if (StatusFilter != AllStatuses && StatusFilter != NotResolved && m.Status != StatusFilter) return false;
        if (!string.IsNullOrWhiteSpace(TextFilter))
        {
            var t = TextFilter.Trim();
            bool Has(string s) => s.Contains(t, StringComparison.CurrentCultureIgnoreCase);
            if (!(Has(m.Subject) || Has(m.Comment) || Has(m.Author) || Has(m.KindName) || Has(m.Status) || Has(m.Page))) return false;
        }
        return true;
    }

    private IEnumerable<MarkupItemViewModel> Sort(IEnumerable<MarkupItemViewModel> q)
    {
        Func<MarkupItemViewModel, IComparable> key = SortColumn switch
        {
            ReviewSortColumn.Type => m => m.KindName,
            ReviewSortColumn.Subject => m => m.Subject,
            ReviewSortColumn.Comment => m => m.Comment,
            ReviewSortColumn.Author => m => m.Author,
            ReviewSortColumn.Status => m => ReviewStatusOrder(m.Status),
            ReviewSortColumn.Modified => m => m.ModifiedSortKey,
            _ => m => m.PageIndex,
        };
        // 2차 기준은 페이지 → 위에서 아래 → 왼쪽에서 오른쪽 (도면을 읽는 순서)
        var ordered = SortDescending ? q.OrderByDescending(key) : q.OrderBy(key);
        return ordered.ThenBy(m => m.PageIndex).ThenBy(m => m.Bounds.Y0).ThenBy(m => m.Bounds.X0);
    }

    private static int ReviewStatusOrder(string s)
    {
        var i = ReviewStatus.All.ToList().IndexOf(s);
        return i < 0 ? 99 : i;
    }

    // ---- 선택 동기화 ----

    /// <summary>화면(DataGrid)에서 행 선택이 바뀌었을 때 호출. 문서 선택을 맞추고, 하나면 그 위치로 이동한다.</summary>
    public void OnRowsSelected(IReadOnlyList<MarkupItemViewModel> rows)
    {
        if (_syncingSelection || _document == null) return;
        _syncingSelection = true;
        try
        {
            SelectedItems.Clear();
            foreach (var r in rows) SelectedItems.Add(r);
            _document.SelectMany(rows);
        }
        finally { _syncingSelection = false; }
        if (rows.Count == 1) NavigateRequested?.Invoke(this, rows[0]);
    }

    private void OnDocumentSelectionChanged(object? sender, EventArgs e) => SyncSelectionFromDocument();

    private void SyncSelectionFromDocument()
    {
        if (_syncingSelection || _document == null) return;
        _syncingSelection = true;
        try
        {
            SelectedItems.Clear();
            foreach (var s in _document.SelectedMarkups.Where(Items.Contains)) SelectedItems.Add(s);
            SelectionSynced?.Invoke(this, EventArgs.Empty);
        }
        finally { _syncingSelection = false; }
    }

    /// <summary>캔버스 선택이 바뀌어 화면의 행 선택을 다시 맞춰야 할 때 (DataGrid가 구독).</summary>
    public event EventHandler? SelectionSynced;

    // ---- 상태 일괄 변경 ----

    [RelayCommand]
    private async Task SetStatusAsync(string status)
    {
        if (_document == null || SelectedItems.Count == 0) return;
        await _document.SetStatusAsync(SelectedItems.ToList(), status);
    }

    // ---- 보고서 ----

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_document == null) return;
        var suggested = Path.GetFileNameWithoutExtension(_document.FileName) + " - review list.csv";
        var path = _dialogs.PickSavePath(suggested, "Export Review List (CSV)");
        if (path == null) return;
        var csv = BuildCsv(Items, _document.FileName, DateTimeOffset.Now, HiddenCount);
        try
        {
            if (_writeFile != null) await _writeFile(path, csv);
            else await File.WriteAllTextAsync(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // Excel이 한글을 읽도록 BOM 포함
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Export failed", ex.Message);
        }
    }

    /// <summary>
    /// CSV 보고서: 도면명·작성 기준 시점을 머리에 적고, 페이지·위치·종류·내용·상태를 행으로 쓴다 (기획서 8장).
    /// </summary>
    public static string BuildCsv(IEnumerable<MarkupItemViewModel> items, string documentName, DateTimeOffset asOf, int hiddenCount = 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv("Document", documentName));
        sb.AppendLine(Csv("Report time", asOf.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)));
        if (hiddenCount > 0) sb.AppendLine(Csv("Note", $"{hiddenCount} markups hidden by the current filter are not included"));
        sb.AppendLine();
        sb.AppendLine(Csv("ID", "Page", "Page label", "Type", "Subject", "Comment", "Author", "Status", "Modified", "Location (x, y pt)"));
        foreach (var m in items)
            sb.AppendLine(Csv(m.Id, (m.PageIndex + 1).ToString(CultureInfo.InvariantCulture), m.PageLabel ?? "", m.KindName, m.Subject, m.Comment,
                m.Author, m.Status, m.Modified,
                string.Create(CultureInfo.InvariantCulture, $"{m.Bounds.X0:0}, {m.Bounds.Y0:0}")));
        return sb.ToString();
    }

    private static string Csv(params string[] fields) =>
        string.Join(",", fields.Select(f => f.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + f.Replace("\"", "\"\"") + "\"" : f));
}
