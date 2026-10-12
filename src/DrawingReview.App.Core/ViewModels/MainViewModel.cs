using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

public enum LeftPanelKind
{
    Thumbnails,
    Bookmarks,
    Search,
    SavedTools,
}

/// <summary>메인 창 전체 상태: 열린 문서 탭, 현재 도구, 패널 선택, 상태 표시줄.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IPdfEngine _engine;
    private readonly IDialogService _dialogs;

    public MainViewModel(IPdfEngine engine, IDialogService dialogs)
    {
        _engine = engine;
        _dialogs = dialogs;
        ReviewList = new ReviewListViewModel(dialogs);
        ReviewList.NavigateRequested += (_, item) => ActiveDocument?.RevealMarkup(item);
        ToolDefaults = new ToolDefaults(Environment.UserName);
    }

    /// <summary>도구별 기본 스타일·작성자. 설정 파일에서 불러오고 저장한다.</summary>
    public ToolDefaults ToolDefaults { get; }

    private readonly Dictionary<DocumentViewModel, MarkupInteraction> _interactions = new();

    /// <summary>현재 탭의 캔버스 마우스 동작 처리기.</summary>
    public MarkupInteraction? Interaction => ActiveDocument is { } d ? InteractionFor(d) : null;

    public MarkupInteraction InteractionFor(DocumentViewModel doc)
    {
        if (_interactions.TryGetValue(doc, out var existing)) return existing;
        var interaction = new MarkupInteraction(doc, ToolDefaults);
        interaction.MarkupCreated += (_, _) => { if (!KeepToolActive) ActiveTool = ToolKind.Select; };
        _interactions[doc] = interaction;
        return interaction;
    }

    /// <summary>연속 배치: 켜면 마크업을 그린 뒤에도 같은 도구가 유지된다 (기획서 7장).</summary>
    [ObservableProperty]
    private bool _keepToolActive;

    private List<MarkupDraft> _clipboard = [];
    private int _clipboardPage = -1;
    private int _pasteCount;

    [RelayCommand]
    private void Copy()
    {
        if (ActiveDocument is not { } doc) return;
        var drafts = doc.SelectedMarkups.Where(m => m.IsEditable).Select(m => m.Draft!).ToList();
        if (drafts.Count == 0) return;
        _clipboard = drafts;
        _clipboardPage = doc.CurrentPageIndex;
        _pasteCount = 0;
        StatusMessage = drafts.Count == 1 ? "Copied 1 markup" : $"Copied {drafts.Count} markups";
    }

    [RelayCommand]
    private async Task CutAsync()
    {
        if (ActiveDocument is not { } doc) return;
        Copy();
        await doc.DeleteMarkupsAsync(doc.SelectedMarkups.ToList());
    }

    /// <summary>붙여넣기: 같은 페이지면 조금씩 비켜서, 다른 페이지면 같은 위치에 놓는다.</summary>
    [RelayCommand]
    private async Task PasteAsync()
    {
        if (ActiveDocument is not { } doc || _clipboard.Count == 0) return;
        var page = doc.CurrentPageIndex;
        double offset = page == _clipboardPage ? 12 / doc.Viewport.Zoom * ++_pasteCount : 0;
        var drafts = _clipboard.Select(d => DraftGeometry.Translate(d, offset, offset) with { Author = ToolDefaults.Author }).ToList();
        var items = await doc.AddMarkupsAsync(page, drafts, drafts.Count == 1 ? "Paste markup" : $"Paste {drafts.Count} markups");
        doc.SelectMany(items);
    }

    public ReviewListViewModel ReviewList { get; }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle), nameof(HasDocument))]
    private DocumentViewModel? _activeDocument;

    public bool HasDocument => ActiveDocument != null;

    public string WindowTitle => ActiveDocument is { } d ? $"Drawing Review — {d.Title}" : "Drawing Review";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveToolName))]
    private ToolKind _activeTool = ToolKind.Select;

    public string ActiveToolName => ToolInfo.DisplayName(ActiveTool);

    [ObservableProperty]
    private LeftPanelKind _leftPanel = LeftPanelKind.Thumbnails;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLeftPanelVisible = true;

    [ObservableProperty]
    private bool _isRightPanelVisible = true;

    [ObservableProperty]
    private bool _isReviewListVisible = true;

    [RelayCommand]
    private void ToggleLeftPanel() => IsLeftPanelVisible = !IsLeftPanelVisible;

    [RelayCommand]
    private void ToggleRightPanel() => IsRightPanelVisible = !IsRightPanelVisible;

    [RelayCommand]
    private void ToggleReviewList() => IsReviewListVisible = !IsReviewListVisible;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (ActiveDocument is { } doc) await SaveDocumentAsync(doc);
    }

    /// <summary>다른 이름으로 저장. 원본은 그대로 두고 새 파일에 쓴 뒤, 탭이 새 파일을 가리킨다.</summary>
    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (ActiveDocument is not { } doc) return;
        var path = _dialogs.PickSavePath(doc.FileName);
        if (path == null) return;
        if (await SaveDocumentAsync(doc, path)) doc.RenameTo(path);
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnDocumentPropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnDocumentPropertyChanged;
        ReviewList.Document = newValue;
        OnPropertyChanged(nameof(Interaction));
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.Title)) OnPropertyChanged(nameof(WindowTitle));
    }

    [RelayCommand]
    private void SelectTool(ToolKind tool) => ActiveTool = tool;

    /// <summary>Esc: 그리던 것을 취소하고, 이미 아무것도 안 하고 있으면 선택 도구로 돌아가고 선택을 푼다.</summary>
    [RelayCommand]
    private void CancelTool()
    {
        if (Interaction is { IsBusy: true } i) { i.Cancel(); return; }
        if (ActiveTool != ToolKind.Select) ActiveTool = ToolKind.Select;
        else ActiveDocument?.ClearSelection();
    }

    [RelayCommand]
    private void ShowLeftPanel(LeftPanelKind panel) => LeftPanel = panel;

    [RelayCommand]
    private async Task OpenAsync()
    {
        foreach (var path in _dialogs.PickPdfFilesToOpen())
            await OpenPathAsync(path);
    }

    /// <summary>파일을 열어 탭으로 추가한다. 이미 열려 있으면 그 탭으로 이동한다. 실패하면 null.</summary>
    public async Task<DocumentViewModel?> OpenPathAsync(string path)
    {
        var full = Path.GetFullPath(path);
        var existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveDocument = existing;
            return existing;
        }

        IPdfDocument? pdf = null;
        try
        {
            StatusMessage = $"Opening {Path.GetFileName(full)}…";
            pdf = await Task.Run(() => _engine.Open(full));
            if (!Unlock(pdf, full))
            {
                pdf.Dispose();
                StatusMessage = "Open cancelled";
                return null;
            }
            var doc = new DocumentViewModel(pdf, full);
            await doc.LoadMarkupsAsync();
            Documents.Add(doc);
            ActiveDocument = doc;
            StatusMessage = $"Opened {doc.FileName} ({doc.Pages.Count} pages)";
            var warnings = pdf.Security.Warnings().Where(w => !w.Contains("password", StringComparison.OrdinalIgnoreCase)).ToList();
            if (warnings.Count > 0)
                _dialogs.ShowInfo("Document notice", string.Join(Environment.NewLine, warnings));
            _ = doc.LoadThumbnailsAsync();
            return doc;
        }
        // 파일 열기는 사용자 입력 경계라 엔진이 던지는 모든 오류를 메시지로 보여준다 (엔진 종류에 묶이지 않도록 구체 타입을 쓰지 않음).
        catch (Exception ex)
        {
            pdf?.Dispose();
            StatusMessage = "Could not open file";
            _dialogs.ShowError("Could not open PDF", $"{Path.GetFileName(full)}\n\n{ex.Message}");
            return null;
        }
    }

    private bool Unlock(IPdfDocument pdf, string path)
    {
        if (!pdf.Security.NeedsPassword) return true;
        var retry = false;
        while (true)
        {
            var password = _dialogs.PromptPassword(Path.GetFileName(path), retry);
            if (password == null) return false;
            if (pdf.Authenticate(password)) return true;
            retry = true;
        }
    }

    /// <summary>탭을 닫는다. 저장하지 않은 변경이 있으면 묻는다. 닫았으면 true.</summary>
    [RelayCommand]
    public async Task<bool> CloseDocumentAsync(DocumentViewModel? doc)
    {
        doc ??= ActiveDocument;
        if (doc == null) return true;
        if (doc.IsModified)
        {
            var answer = _dialogs.Confirm("Unsaved changes", $"Save changes to {doc.FileName} before closing?");
            if (answer == null) return false;
            if (answer == true && !await SaveDocumentAsync(doc)) return false;
        }
        var index = Documents.IndexOf(doc);
        _interactions.Remove(doc);
        Documents.Remove(doc);
        doc.Dispose();
        if (ActiveDocument == doc || ActiveDocument == null)
            ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
        return true;
    }

    /// <summary>창을 닫기 전: 모든 탭을 닫을 수 있으면 true.</summary>
    public async Task<bool> CloseAllAsync()
    {
        foreach (var doc in Documents.ToList())
            if (!await CloseDocumentAsync(doc)) return false;
        return true;
    }

    /// <summary>저장 (2단계에서 마크업 편집과 함께 확장). 성공하면 true.</summary>
    public async Task<bool> SaveDocumentAsync(DocumentViewModel doc, string? path = null)
    {
        path ??= doc.FilePath;
        try
        {
            StatusMessage = $"Saving {Path.GetFileName(path)}…";
            await doc.Gate.RunAsync(() => doc.Document.SaveTo(path));
            doc.IsModified = false;
            StatusMessage = $"Saved {Path.GetFileName(path)} at {DateTime.Now:HH:mm}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed — original file unchanged";
            _dialogs.ShowError("Save failed", $"The original file was not changed.\n\n{ex.Message}");
            return false;
        }
    }
}
