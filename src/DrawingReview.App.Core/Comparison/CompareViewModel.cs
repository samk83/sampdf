using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.Comparison;

/// <summary>
/// 도면 비교 작업 공간 (Drawing Difference Checker 기획서 4·5장).
/// 원본 보호를 위해 두 PDF를 따로 열고, Revision Cloud는 개정 도면의 사본(메모리)에 추가한다.
/// 내보낼 때만 사용자가 고른 새 파일에 저장한다.
/// </summary>
public sealed partial class CompareViewModel : ObservableObject, IDisposable
{
    private readonly IPdfEngine _engine;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<PagePair, PageState> _pageStates = new();
    private CancellationTokenSource? _cts;

    private sealed class PageState
    {
        public CompareSettings Settings = new();
        public AlignmentResult? Alignment;
        public List<ChangeItemViewModel> Changes = [];
    }

    public CompareViewModel(IPdfEngine engine, IDialogService dialogs)
    {
        _engine = engine;
        _dialogs = dialogs;
    }

    /// <summary>비교 창의 도구: 선택·이동, 손, 수동 구름·사각형.</summary>
    [ObservableProperty] private ToolKind _activeTool = ToolKind.Select;

    /// <summary>수동 Revision Cloud용 도구 기본값 (Cloud 설정의 색·굵기를 따른다).</summary>
    public ToolDefaults ToolDefaults { get; } = new(Environment.UserName);

    /// <summary>개정 도면 사본 위의 마크업 편집 (Cloud 이동·크기 조절, 수동 Cloud 그리기).</summary>
    [ObservableProperty] private MarkupInteraction? _interaction;

    partial void OnCloudOptionsChanged(CloudOptions value)
    {
        var style = MarkupStyle.Default with { StrokeColor = value.Color, TextColor = value.Color, LineWidth = value.LineWidth };
        ToolDefaults.Set(ToolKind.Cloud, new ToolPreset(style, "Revision"));
        ToolDefaults.Set(ToolKind.Rectangle, new ToolPreset(style, "Revision"));
    }

    public DocumentViewModel? OldDocument { get; private set; }
    public DocumentViewModel? NewDocument { get; private set; }
    public string OldPath { get; private set; } = "";
    public string NewPath { get; private set; } = "";

    public ObservableCollection<PagePair> PagePairs { get; } = [];
    public ObservableCollection<ChangeItemViewModel> Changes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPair))]
    private PagePair? _selectedPair;

    public bool HasPair => SelectedPair != null;

    [ObservableProperty] private CompareViewMode _mode = CompareViewMode.Overlay;
    [ObservableProperty] private bool _syncNavigation = true;
    [ObservableProperty] private bool _highSensitivity;
    [ObservableProperty] private bool _ignoreTitleBlock;
    [ObservableProperty] private CloudOptions _cloudOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlignmentText), nameof(AlignmentReliable))]
    private AlignmentResult? _alignment;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Open two PDFs to compare.";
    [ObservableProperty] private ChangeItemViewModel? _selectedChange;

    /// <summary>겹쳐 보기·차이만 보기 그림 (현재 짝, 개정 도면 좌표).</summary>
    [ObservableProperty] private RenderedImage? _overlayImage;

    public string AlignmentText => Alignment is { } a
        ? $"{a.Method} · quality {a.Quality:0.00} · scale {a.Transform.Scale:0.000} · rotation {a.Transform.RotationDegrees:0.00}°" + (a.IsReliable ? "" : " — check alignment")
        : "Not aligned";

    public bool AlignmentReliable => Alignment?.IsReliable ?? false;

    public string ChangeCountText => $"{Changes.Count(c => c.Status != ChangeStatus.Dismissed)} changes" +
        (Changes.Any(c => c.Status == ChangeStatus.Dismissed) ? $" ({Changes.Count(c => c.Status == ChangeStatus.Dismissed)} dismissed)" : "");

    /// <summary>수동 정렬 중 지금까지 찍은 점 (이전1, 개정1, 이전2, 개정2 순서).</summary>
    public ObservableCollection<PagePoint> ManualPoints { get; } = [];

    [ObservableProperty] private bool _isPickingAlignment;

    public string ManualAlignmentHint => ManualPoints.Count switch
    {
        0 => "Click point 1 on the OLD drawing",
        1 => "Click the same point 1 on the NEW drawing",
        2 => "Click point 2 on the OLD drawing (far from point 1)",
        3 => "Click the same point 2 on the NEW drawing",
        _ => "",
    };

    // ---- 열기 ----

    /// <summary>두 PDF를 연다. 원본은 읽기만 한다.</summary>
    public async Task<bool> OpenAsync(string oldPath, string newPath)
    {
        try
        {
            Status = "Opening drawings…";
            var oldPdf = await Task.Run(() => _engine.Open(oldPath));
            var newPdf = await Task.Run(() => _engine.Open(newPath));
            if (oldPdf.Security.NeedsPassword || newPdf.Security.NeedsPassword)
            {
                oldPdf.Dispose();
                newPdf.Dispose();
                _dialogs.ShowError("Cannot compare", "Password-protected PDFs are not supported in comparison yet. Remove the password first.");
                return false;
            }
            OldDocument?.Dispose();
            NewDocument?.Dispose();
            OldPath = Path.GetFullPath(oldPath);
            NewPath = Path.GetFullPath(newPath);
            OldDocument = new DocumentViewModel(oldPdf, OldPath);
            NewDocument = new DocumentViewModel(newPdf, NewPath);
            await NewDocument.LoadMarkupsAsync();
            OnCloudOptionsChanged(CloudOptions);
            Interaction = new MarkupInteraction(NewDocument, ToolDefaults);
            Interaction.MarkupCreated += (_, _) => ActiveTool = ToolKind.Select;
            OnPropertyChanged(nameof(OldDocument));
            OnPropertyChanged(nameof(NewDocument));

            _pageStates.Clear();
            PagePairs.Clear();
            var n = Math.Min(OldDocument.Pages.Count, NewDocument.Pages.Count);
            for (int i = 0; i < n; i++) PagePairs.Add(new PagePair(i, i));
            SelectedPair = PagePairs.FirstOrDefault();
            Status = OldDocument.Pages.Count == NewDocument.Pages.Count
                ? $"Opened {OldDocument.FileName} and {NewDocument.FileName} ({n} page pairs)."
                : $"Page counts differ ({OldDocument.Pages.Count} vs {NewDocument.Pages.Count}). Pairs were matched by page number — adjust them if needed.";
            return true;
        }
        catch (Exception ex)
        {
            Status = "Could not open drawings.";
            _dialogs.ShowError("Could not open drawings", ex.Message);
            return false;
        }
    }

    /// <summary>페이지 짝을 바꾸거나 추가한다.</summary>
    public void SetPair(int index, PagePair pair)
    {
        if (OldDocument == null || NewDocument == null) return;
        if (pair.OldPage < 0 || pair.OldPage >= OldDocument.Pages.Count || pair.NewPage < 0 || pair.NewPage >= NewDocument.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pair));
        if (index >= 0 && index < PagePairs.Count) PagePairs[index] = pair;
        else PagePairs.Add(pair);
        SelectedPair = pair;
    }

    partial void OnSelectedPairChanged(PagePair? value)
    {
        Changes.Clear();
        Alignment = null;
        OverlayImage = null;
        if (value == null || OldDocument == null || NewDocument == null) return;
        OldDocument.GoToPage(value.OldPage);
        NewDocument.GoToPage(value.NewPage);
        if (_pageStates.TryGetValue(value, out var st))
        {
            Alignment = st.Alignment;
            foreach (var c in st.Changes) Changes.Add(c);
            _ = RefreshOverlayAsync();
        }
        OnPropertyChanged(nameof(ChangeCountText));
    }

    partial void OnModeChanged(CompareViewMode value) => _ = RefreshOverlayAsync();

    // ---- 비교 실행 ----

    private CompareSettings CurrentSettings()
    {
        var s = HighSensitivity ? CompareSettings.HighSensitivity : new CompareSettings();
        if (IgnoreTitleBlock && NewDocument?.CurrentPage is { } p)
        {
            // 일반적인 도면 제목란 위치: 오른쪽 아래 (가로 30%, 세로 18%)
            var w = p.Info.Width;
            var h = p.Info.Height;
            s = s with { IgnoreRegions = [new PageRect(w * 0.70, h * 0.82, w, h)] };
        }
        return s;
    }

    private (PageSource Old, PageSource New) Sources(PagePair pair)
    {
        var oldDoc = OldDocument!;
        var newDoc = NewDocument!;
        var oi = oldDoc.Pages[pair.OldPage].Info;
        var ni = newDoc.Pages[pair.NewPage].Info;
        // 비교 엔진은 백그라운드에서 동기적으로 렌더링을 부른다. 각 문서의 순서 보장(EngineGate)을 지키도록 그 안에서 실행한다.
        PageSource Make(DocumentViewModel d, int page, PageInfo info) => new(info.Width, info.Height,
            zoom => d.Gate.RunAsync(() => d.Document.RenderPage(page, zoom, includeMarkups: false)).GetAwaiter().GetResult(),
            () => d.Gate.RunAsync(() => d.Document.GetWords(page)).GetAwaiter().GetResult());
        return (Make(oldDoc, pair.OldPage, oi), Make(newDoc, pair.NewPage, ni));
    }

    [RelayCommand]
    public async Task RunCompareAsync()
    {
        if (SelectedPair is not { } pair || OldDocument == null || NewDocument == null) return;
        await RunAsync(pair, CurrentSettings(), manualAlignment: null);
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private async Task RunAsync(PagePair pair, CompareSettings settings, AlignmentResult? manualAlignment)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        IsBusy = true;
        Status = "Aligning and comparing…";
        try
        {
            var (o, n) = Sources(pair);
            var result = await Task.Run(() => PageComparer.Compare(o, n, settings, manualAlignment), cts.Token);
            if (cts.IsCancellationRequested) return;
            var state = new PageState
            {
                Settings = settings,
                Alignment = result.Alignment,
                Changes = result.Regions.Select(r => new ChangeItemViewModel(r)).ToList(),
            };
            _pageStates[pair] = state;
            if (SelectedPair == pair)
            {
                Alignment = result.Alignment;
                Changes.Clear();
                foreach (var c in state.Changes) Changes.Add(c);
                OnPropertyChanged(nameof(ChangeCountText));
                await RefreshOverlayAsync();
            }
            Status = $"Found {result.Regions.Count} change candidates in {result.Duration.TotalSeconds:0.0} s." +
                (result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : "");
        }
        catch (OperationCanceledException)
        {
            Status = "Comparison cancelled.";
        }
        catch (Exception ex)
        {
            Status = "Comparison failed.";
            _dialogs.ShowError("Comparison failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>겹쳐 보기 그림을 다시 만든다 (모드·정렬이 바뀌었을 때).</summary>
    public async Task RefreshOverlayAsync()
    {
        if (SelectedPair is not { } pair || Alignment is not { } a || OldDocument == null || NewDocument == null || Mode == CompareViewMode.SideBySide)
        {
            OverlayImage = null;
            return;
        }
        var (o, n) = Sources(pair);
        var zoom = DocumentViewModel.PreviewLongEdge * 1.5 / Math.Max(n.Width, n.Height);
        var showCommon = Mode == CompareViewMode.Overlay;
        OverlayImage = await Task.Run(() => PageComparer.RenderOverlay(o, n, a.Transform, zoom, showCommon: showCommon));
    }

    // ---- 수동 2점 정렬 ----

    [RelayCommand]
    private void BeginManualAlignment()
    {
        ManualPoints.Clear();
        IsPickingAlignment = true;
        Mode = CompareViewMode.SideBySide;
        OnPropertyChanged(nameof(ManualAlignmentHint));
    }

    /// <summary>나란히 보기에서 점을 찍는다. isOld: 이전 도면 화면에서 찍었는지. 네 점이 모이면 정렬하고 다시 비교한다.</summary>
    public async Task AddAlignmentPointAsync(bool isOld, PagePoint p)
    {
        if (!IsPickingAlignment || SelectedPair is not { } pair) return;
        var expectOld = ManualPoints.Count % 2 == 0;
        if (isOld != expectOld) return; // 순서대로 찍게 한다
        ManualPoints.Add(p);
        OnPropertyChanged(nameof(ManualAlignmentHint));
        if (ManualPoints.Count < 4) return;

        IsPickingAlignment = false;
        try
        {
            var t = AlignmentTransform.FromTwoPoints(ManualPoints[0], ManualPoints[2], ManualPoints[1], ManualPoints[3]);
            var (o, n) = Sources(pair);
            var manual = await Task.Run(() => Aligner.Manual(o, n, t));
            await RunAsync(pair, CurrentSettings(), manual);
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
        }
        finally
        {
            ManualPoints.Clear();
            OnPropertyChanged(nameof(ManualAlignmentHint));
        }
    }

    [RelayCommand]
    private void CancelManualAlignment()
    {
        IsPickingAlignment = false;
        ManualPoints.Clear();
        OnPropertyChanged(nameof(ManualAlignmentHint));
    }

    // ---- 변경 검토 ----

    private IEnumerable<ChangeItemViewModel> Targets(ChangeItemViewModel? item) =>
        item != null ? [item] : SelectedChange != null ? [SelectedChange] : [];

    [RelayCommand]
    private void Accept(ChangeItemViewModel? item) { foreach (var c in Targets(item)) c.Status = ChangeStatus.Accepted; OnPropertyChanged(nameof(ChangeCountText)); }

    [RelayCommand]
    private void Dismiss(ChangeItemViewModel? item) { foreach (var c in Targets(item)) c.Status = ChangeStatus.Dismissed; OnPropertyChanged(nameof(ChangeCountText)); }

    [RelayCommand]
    private void ResetStatus(ChangeItemViewModel? item) { foreach (var c in Targets(item)) c.Status = ChangeStatus.Pending; OnPropertyChanged(nameof(ChangeCountText)); }

    [RelayCommand]
    private void AcceptAllHighConfidence()
    {
        foreach (var c in Changes.Where(c => c.Confidence == ChangeConfidence.High && c.Status == ChangeStatus.Pending)) c.Status = ChangeStatus.Accepted;
        OnPropertyChanged(nameof(ChangeCountText));
    }

    [RelayCommand]
    private void ToggleVisibility(ChangeItemViewModel? item) { foreach (var c in Targets(item)) c.IsVisible = !c.IsVisible; }

    /// <summary>선택한 변경들을 하나로 합친다.</summary>
    public void Merge(IReadOnlyList<ChangeItemViewModel> items)
    {
        if (items.Count < 2 || SelectedPair is not { } pair) return;
        var merged = new ChangeItemViewModel(PageComparer.Merge(items.Select(i => i.Region).ToList(), items.Min(i => i.Number)))
        {
            Status = items.Any(i => i.Status == ChangeStatus.Accepted) ? ChangeStatus.Accepted : ChangeStatus.Pending,
        };
        var index = items.Min(i => Changes.IndexOf(i));
        foreach (var i in items) Changes.Remove(i);
        Changes.Insert(Math.Clamp(index, 0, Changes.Count), merged);
        Renumber(pair);
        SelectedChange = merged;
    }

    /// <summary>한 변경을 더 작은 영역들로 나눈다. 나눌 것이 없으면 그대로 둔다.</summary>
    public async Task SplitAsync(ChangeItemViewModel item)
    {
        if (SelectedPair is not { } pair || Alignment is not { } a || !_pageStates.TryGetValue(pair, out var st)) return;
        var (o, n) = Sources(pair);
        IsBusy = true;
        try
        {
            var parts = await Task.Run(() => PageComparer.Split(o, n, st.Settings, a, item.Bounds));
            if (parts.Count < 2) { Status = "This change cannot be split further."; return; }
            var index = Changes.IndexOf(item);
            Changes.Remove(item);
            foreach (var (part, k) in parts.Select((p, k) => (p, k)))
                Changes.Insert(index + k, new ChangeItemViewModel(part) { Status = item.Status });
            Renumber(pair);
            Status = $"Split into {parts.Count} changes.";
        }
        finally { IsBusy = false; }
    }

    /// <summary>사용자가 직접 그린 영역을 변경으로 추가한다 (수동 Revision Cloud).</summary>
    public ChangeItemViewModel AddManualChange(PageRect bounds)
    {
        var inverse = Alignment?.Transform.Inverse() ?? AlignmentTransform.Identity;
        var item = new ChangeItemViewModel(new ChangeRegion(Changes.Count + 1, ChangeKind.Modified, bounds, inverse.Apply(bounds), 0, 0, ChangeConfidence.High))
        {
            Status = ChangeStatus.Accepted,
        };
        Changes.Add(item);
        if (SelectedPair is { } pair) Renumber(pair);
        return item;
    }

    private void Renumber(PagePair pair)
    {
        var ordered = Changes.OrderBy(c => Math.Round(c.Bounds.Y0 / 50)).ThenBy(c => c.Bounds.X0).ToList();
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].Region = ordered[i].Region with { Number = i + 1 };
        Changes.Clear();
        foreach (var c in ordered) Changes.Add(c);
        if (_pageStates.TryGetValue(pair, out var st)) st.Changes = ordered;
        else _pageStates[pair] = new PageState { Alignment = Alignment, Changes = ordered };
        OnPropertyChanged(nameof(ChangeCountText));
    }

    /// <summary>선택한 변경으로 이동 (두 화면 모두).</summary>
    [RelayCommand]
    private void Navigate(ChangeItemViewModel? item)
    {
        item ??= SelectedChange;
        if (item == null || NewDocument == null || OldDocument == null) return;
        SelectedChange = item;
        NewDocument.Viewport.ZoomToRegion(item.Bounds.Inflate(Math.Max(item.Bounds.Width, item.Bounds.Height) + 20));
        OldDocument.Viewport.ZoomToRegion(item.BoundsInOld.Inflate(Math.Max(item.BoundsInOld.Width, item.BoundsInOld.Height) + 20));
    }

    // ---- Revision Cloud ----

    /// <summary>
    /// 수락한 변경마다 Revision Cloud를 개정 도면(사본)에 만든다. 이미 만든 것은 건너뛴다.
    /// 구름은 표준 PDF 주석이라 내보낸 뒤에도 다른 뷰어에서 편집할 수 있다.
    /// </summary>
    [RelayCommand]
    public async Task CreateCloudsAsync()
    {
        if (NewDocument is not { } doc || SelectedPair is not { } pair) return;
        var targets = Changes.Where(c => c.Status == ChangeStatus.Accepted && c.CloudMarkupId == null).ToList();
        if (targets.Count == 0) { Status = "Accept changes first, then create clouds."; return; }
        var o = CloudOptions;
        var style = MarkupStyle.Default with { StrokeColor = o.Color, TextColor = o.Color, LineWidth = o.LineWidth, FontSize = 10 };
        var page = pair.NewPage;
        var info = doc.Pages[page].Info;
        foreach (var c in targets)
        {
            var r = c.Bounds.Inflate(o.MarginPt);
            r = new PageRect(Math.Max(0, r.X0), Math.Max(0, r.Y0), Math.Min(info.Width, r.X1), Math.Min(info.Height, r.Y1));
            MarkupDraft shape = o.Shape == CloudShape.Cloud
                ? CloudDraft.FromRect(r, o.ArcSize) with { Style = style }
                : new RectangleDraft(r) { Style = style };
            shape = shape with { Subject = "Revision", Comment = $"Change {c.Number}: {c.Kind}", Author = Environment.UserName };
            var drafts = new List<MarkupDraft> { shape };
            if (o.Numbering)
            {
                var label = new PageRect(r.X1 + 2, Math.Max(0, r.Y0 - 16), r.X1 + 26, Math.Max(16, r.Y0));
                drafts.Add(new TextBoxDraft(label, c.Number.ToString(CultureInfo.InvariantCulture))
                    { Style = style with { FillColor = new RgbColor(1, 1, 1) }, Subject = "Revision number", Author = Environment.UserName });
            }
            var items = await doc.AddMarkupsAsync(page, drafts, $"Revision cloud {c.Number}");
            c.CloudMarkupId = items[0].Id;
        }
        Status = $"Created {targets.Count} revision clouds on the revised drawing copy. Export to save them as a PDF.";
    }

    // ---- 내보내기·저장 ----

    /// <summary>Cloud가 들어간 개정 도면을 새 PDF로 내보낸다. 원본 개정 도면 파일은 바꾸지 않는다.</summary>
    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (NewDocument is not { } doc) return;
        var path = _dialogs.PickSavePath(Path.GetFileNameWithoutExtension(NewPath) + " (compared).pdf", "Export annotated PDF");
        if (path == null) return;
        if (SamePath(path, NewPath) || SamePath(path, OldPath))
        {
            _dialogs.ShowError("Choose another file", "Original drawings are never overwritten. Choose a new file name.");
            return;
        }
        try
        {
            await doc.Gate.RunAsync(() => doc.Document.SaveTo(path));
            Status = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Export failed", ex.Message);
        }
    }

    [RelayCommand]
    public async Task ExportChangeListAsync()
    {
        if (SelectedPair == null) return;
        var path = _dialogs.PickSavePath(Path.GetFileNameWithoutExtension(NewPath) + " - changes.csv", "Export change list (CSV or JSON)");
        if (path == null) return;
        var text = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? BuildJson() : BuildCsv();
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(true));
        Status = $"Exported change list: {Path.GetFileName(path)}";
    }

    [RelayCommand]
    public async Task ExportImageAsync()
    {
        if (SelectedPair is not { } pair || Alignment is not { } a) return;
        var path = _dialogs.PickSavePath(Path.GetFileNameWithoutExtension(NewPath) + " - comparison.png", "Export comparison image");
        if (path == null) return;
        var (o, n) = Sources(pair);
        var png = await Task.Run(() => PageComparer.EncodePng(PageComparer.RenderOverlay(o, n, a.Transform, 200 / 72.0)));
        await File.WriteAllBytesAsync(path, png);
        Status = $"Exported comparison image: {Path.GetFileName(path)}";
    }

    public string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Old,{Csv(OldPath)}");
        sb.AppendLine($"New,{Csv(NewPath)}");
        sb.AppendLine($"Pages,{SelectedPair}");
        sb.AppendLine($"Alignment,{Csv(AlignmentText)}");
        sb.AppendLine($"Report time,{DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
        sb.AppendLine();
        sb.AppendLine("Number,Kind,Confidence,Status,X0,Y0,X1,Y1,Old X0,Old Y0,Old X1,Old Y1");
        foreach (var c in Changes)
            sb.AppendLine(string.Join(",", c.Number, c.Kind, c.Confidence, c.Status,
                F(c.Bounds.X0), F(c.Bounds.Y0), F(c.Bounds.X1), F(c.Bounds.Y1),
                F(c.BoundsInOld.X0), F(c.BoundsInOld.Y0), F(c.BoundsInOld.X1), F(c.BoundsInOld.Y1)));
        return sb.ToString();
    }

    public string BuildJson() => JsonSerializer.Serialize(new
    {
        old = OldPath,
        @new = NewPath,
        pages = SelectedPair?.ToString(),
        alignment = Alignment,
        changes = Changes.Select(c => new { c.Number, Kind = c.Kind.ToString(), Confidence = c.Confidence.ToString(), Status = c.Status.ToString(), c.Bounds, c.BoundsInOld }),
    }, CompareProject.Json);

    /// <summary>비교 프로젝트 저장 (경로·짝·설정·정렬·변경 상태).</summary>
    public void SaveProject(string path)
    {
        var project = new CompareProject { OldPath = OldPath, NewPath = NewPath, Clouds = CloudOptions };
        foreach (var pair in PagePairs)
        {
            var entry = new CompareProject.PageEntry { OldPage = pair.OldPage, NewPage = pair.NewPage };
            if (_pageStates.TryGetValue(pair, out var st))
            {
                entry.Settings = st.Settings;
                entry.Alignment = st.Alignment;
                entry.Changes = st.Changes.Select(c => c.Region with { Status = c.Status }).ToList();
                entry.Clouds = st.Changes.Where(c => c.CloudMarkupId != null).ToDictionary(c => c.Number, c => c.CloudMarkupId!);
                entry.Hidden = st.Changes.Where(c => !c.IsVisible).Select(c => c.Number).ToList();
            }
            project.Pages.Add(entry);
        }
        project.Save(path);
        Status = $"Saved comparison project {Path.GetFileName(path)}.";
    }

    [RelayCommand]
    private void SaveProjectAs()
    {
        var path = _dialogs.PickSavePath(Path.GetFileNameWithoutExtension(NewPath) + CompareProject.Extension, "Save comparison project");
        if (path != null) SaveProject(path);
    }

    /// <summary>저장한 비교 프로젝트를 연다. 결과를 다시 계산하지 않고 저장된 상태를 보여준다.</summary>
    public async Task<bool> LoadProjectAsync(string path)
    {
        var project = CompareProject.Load(path);
        if (!File.Exists(project.OldPath) || !File.Exists(project.NewPath))
        {
            _dialogs.ShowError("Drawing not found", $"The project refers to files that no longer exist:\n{project.OldPath}\n{project.NewPath}");
            return false;
        }
        if (!await OpenAsync(project.OldPath, project.NewPath)) return false;
        CloudOptions = project.Clouds;
        PagePairs.Clear();
        _pageStates.Clear();
        foreach (var e in project.Pages)
        {
            var pair = new PagePair(e.OldPage, e.NewPage);
            PagePairs.Add(pair);
            _pageStates[pair] = new PageState
            {
                Settings = e.Settings,
                Alignment = e.Alignment,
                Changes = e.Changes.Select(r => new ChangeItemViewModel(r)
                {
                    CloudMarkupId = e.Clouds.GetValueOrDefault(r.Number),
                    IsVisible = !e.Hidden.Contains(r.Number),
                }).ToList(),
            };
        }
        SelectedPair = null;
        SelectedPair = PagePairs.FirstOrDefault();
        Status = $"Opened comparison project {Path.GetFileName(path)} (saved {project.SavedAt:yyyy-MM-dd HH:mm}).";
        return true;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Csv(string s) => s.IndexOfAny([',', '"', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public void Dispose()
    {
        _cts?.Cancel();
        OldDocument?.Dispose();
        NewDocument?.Dispose();
    }
}
