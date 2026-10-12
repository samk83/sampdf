using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>색상 견본 하나.</summary>
public sealed record ColorSwatch(string Name, RgbColor Color);

/// <summary>
/// 오른쪽 속성 패널 (기획서 5장).
/// - 마크업을 선택했으면: 선택한 모든 마크업의 색·굵기·불투명도·글자 크기·주제·작성자·내용·상태·잠금을 바꾼다.
/// - 선택이 없고 그리기 도구가 켜져 있으면: 그 도구의 기본값을 바꾼다 (다음에 그릴 마크업에 적용).
/// 바꾼 스타일은 그 도구의 기본값에도 기억한다 (최근 사용한 색·굵기 기억 — 기획서 7장).
/// </summary>
public sealed partial class PropertiesViewModel : ObservableObject
{
    public static readonly IReadOnlyList<ColorSwatch> Palette =
    [
        new("Red", new RgbColor(0.898, 0.216, 0.173)),
        new("Orange", new RgbColor(0.941, 0.549, 0.180)),
        new("Yellow", new RgbColor(1, 0.902, 0)),
        new("Green", new RgbColor(0.180, 0.643, 0.310)),
        new("Blue", new RgbColor(0.184, 0.435, 0.871)),
        new("Magenta", new RgbColor(0.761, 0.220, 0.620)),
        new("Black", new RgbColor(0, 0, 0)),
    ];

    public static readonly IReadOnlyList<double> LineWidths = [0.5, 1, 1.5, 2, 3, 4, 6];
    public static readonly IReadOnlyList<double> FontSizes = [8, 10, 12, 14, 18, 24, 36];

    private readonly MainViewModel _main;
    private bool _loading;
    private DocumentViewModel? _doc;

    public PropertiesViewModel(MainViewModel main)
    {
        _main = main;
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ActiveDocument)) AttachDocument(main.ActiveDocument);
            if (e.PropertyName is nameof(MainViewModel.ActiveTool)) Load();
        };
        main.ToolDefaults.Changed += (_, _) => { if (Mode == PropertiesMode.ToolDefaults) Load(); };
        AttachDocument(main.ActiveDocument);
    }

    public enum PropertiesMode { None, Selection, ToolDefaults }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget), nameof(IsSelectionMode))]
    private PropertiesMode _mode;

    public bool HasTarget => Mode != PropertiesMode.None;
    public bool IsSelectionMode => Mode == PropertiesMode.Selection;

    [ObservableProperty] private string _header = "Nothing selected";
    [ObservableProperty] private RgbColor _strokeColor = RgbColor.Red;
    [ObservableProperty] private double _lineWidth = 1.5;
    [ObservableProperty] private double _opacityPercent = 100;
    [ObservableProperty] private double _fontSize = 12;
    [ObservableProperty] private bool _showFontSize;
    [ObservableProperty] private bool _showLineWidth = true;
    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _comment = "";
    [ObservableProperty] private string _commentLabel = "Comment";
    [ObservableProperty] private string _status = ReviewStatus.Open;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private string _hint = "Select a markup to edit its properties, or pick a drawing tool to set its defaults.";

    public IReadOnlyList<string> Statuses => ReviewStatus.All;

    /// <summary>마지막으로 시작한 변경 작업 (테스트에서 끝날 때까지 기다리는 데 쓴다).</summary>
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    /// <summary>최근 사용한 색 (앞이 최신, 최대 5개).</summary>
    public ObservableCollection<RgbColor> RecentColors { get; } = [];

    private void AttachDocument(DocumentViewModel? doc)
    {
        if (_doc != null) { _doc.SelectionChanged -= OnSelectionChanged; _doc.MarkupsChanged -= OnSelectionChanged; }
        _doc = doc;
        if (_doc != null) { _doc.SelectionChanged += OnSelectionChanged; _doc.MarkupsChanged += OnSelectionChanged; }
        Load();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => Load();

    private List<MarkupItemViewModel> Targets => _doc?.SelectedMarkups.ToList() ?? [];

    /// <summary>선택 또는 도구 기본값을 읽어 화면 값을 채운다.</summary>
    public void Load()
    {
        _loading = true;
        try
        {
            var sel = Targets;
            if (sel.Count > 0)
            {
                Mode = PropertiesMode.Selection;
                var first = sel[0];
                Header = sel.Count == 1 ? $"{first.KindName} · 1 selected" : $"{sel.Count} markups selected";
                var editable = sel.Where(s => s.IsEditable).ToList();
                CanEdit = editable.Any(s => !s.IsLocked);
                var style = editable.FirstOrDefault()?.Draft?.Style ?? MarkupStyle.Default;
                LoadStyle(style, first.Kind);
                Subject = first.Subject;
                Author = first.Author;
                var isText = first.Kind is MarkupKind.TextBox or MarkupKind.Callout;
                CommentLabel = isText ? "Text" : "Comment";
                Comment = sel.Count == 1 ? first.Comment : "";
                Status = sel.Select(s => s.Status).Distinct().Count() == 1 ? first.Status : "";
                IsLocked = sel.All(s => s.IsLocked);
                Hint = !editable.Any() ? "This annotation was made by another program; only its status can be changed."
                    : !CanEdit ? "Locked markups cannot be changed. Unlock to edit." : "";
            }
            else if (_main.HasDocument && ToolInfo.CreatesMarkup(_main.ActiveTool))
            {
                Mode = PropertiesMode.ToolDefaults;
                var preset = _main.ToolDefaults.Get(_main.ActiveTool);
                Header = $"{ToolInfo.DisplayName(_main.ActiveTool)} · tool defaults";
                CanEdit = true;
                LoadStyle(preset.Style, KindForTool(_main.ActiveTool));
                Subject = preset.Subject;
                Author = _main.ToolDefaults.Author;
                CommentLabel = "Default text";
                Comment = preset.DefaultText ?? "";
                Hint = "These settings apply to the next markup you draw.";
            }
            else
            {
                Mode = PropertiesMode.None;
                Header = "Nothing selected";
                CanEdit = false;
                Hint = "Select a markup to edit its properties, or pick a drawing tool to set its defaults.";
            }
        }
        finally { _loading = false; }
    }

    private void LoadStyle(MarkupStyle style, MarkupKind kind)
    {
        var isText = kind is MarkupKind.TextBox or MarkupKind.Callout;
        StrokeColor = isText ? style.TextColor : style.StrokeColor;
        LineWidth = style.LineWidth;
        OpacityPercent = Math.Round(style.Opacity * 100);
        FontSize = style.FontSize;
        ShowFontSize = isText;
        ShowLineWidth = kind != MarkupKind.Highlight;
    }

    private static MarkupKind KindForTool(ToolKind tool) => tool switch
    {
        ToolKind.TextBox => MarkupKind.TextBox,
        ToolKind.Callout => MarkupKind.Callout,
        ToolKind.Highlight => MarkupKind.Highlight,
        _ => MarkupKind.Rectangle,
    };

    // ---- 값이 바뀌면 적용 ----

    partial void OnStrokeColorChanged(RgbColor value) => ApplyStyle(s => s with { StrokeColor = value, TextColor = value }, "Change color", remember: value);
    partial void OnLineWidthChanged(double value) => ApplyStyle(s => s with { LineWidth = value }, "Change line width");
    partial void OnOpacityPercentChanged(double value) => ApplyStyle(s => s with { Opacity = Math.Clamp(value / 100, 0.05, 1) }, "Change opacity");
    partial void OnFontSizeChanged(double value) => ApplyStyle(s => s with { FontSize = value }, "Change font size");
    partial void OnSubjectChanged(string value) => ApplyMeta(d => d with { Subject = value }, p => p with { Subject = value }, "Change subject");
    partial void OnAuthorChanged(string value)
    {
        if (_loading) return;
        if (Mode == PropertiesMode.ToolDefaults) { _main.ToolDefaults.Author = value; return; }
        ApplyMeta(d => d with { Author = value }, p => p, "Change author");
    }
    partial void OnCommentChanged(string value) => ApplyMeta(d => d switch
    {
        TextBoxDraft t => t with { Text = value },
        CalloutDraft c => c with { Text = value },
        _ => d with { Comment = value },
    }, p => p with { DefaultText = string.IsNullOrEmpty(value) ? null : value }, "Edit text", singleOnly: true);

    partial void OnStatusChanged(string value)
    {
        if (_loading || _doc == null || Mode != PropertiesMode.Selection || string.IsNullOrEmpty(value)) return;
        PendingOperation = _doc.SetStatusAsync(Targets, value);
    }

    partial void OnIsLockedChanged(bool value)
    {
        if (_loading || _doc == null || Mode != PropertiesMode.Selection) return;
        PendingOperation = _doc.SetLockedAsync(Targets.Where(t => t.IsEditable), value);
    }

    [RelayCommand]
    private void PickColor(ColorSwatch swatch) => StrokeColor = swatch.Color;

    [RelayCommand]
    private void PickRecent(RgbColor color) => StrokeColor = color;

    /// <summary>스타일 변경: 선택한 마크업들(하나의 되돌리기 단위)과 해당 도구 기본값에 적용.</summary>
    private void ApplyStyle(Func<MarkupStyle, MarkupStyle> change, string undoName, RgbColor? remember = null)
    {
        if (_loading) return;
        if (remember is { } c) Remember(c);
        if (Mode == PropertiesMode.ToolDefaults)
        {
            var tool = _main.ActiveTool;
            _main.ToolDefaults.SetStyle(tool, change(_main.ToolDefaults.Get(tool).Style));
            return;
        }
        if (_doc == null) return;
        var changes = Targets.Where(t => t is { IsEditable: true, IsLocked: false })
            .Select(t => (Item: t, Draft: t.Draft! with { Style = change(t.Draft!.Style) }))
            .ToList();
        if (changes.Count == 0) return;
        foreach (var (_, d) in changes)
        {
            var tool = ToolDefaults.ToolFor(d);
            if (tool != ToolKind.Select) _main.ToolDefaults.SetStyle(tool, d.Style);
        }
        PendingOperation = _doc.ReplaceMarkupsAsync(changes, undoName);
    }

    private void ApplyMeta(Func<MarkupDraft, MarkupDraft> change, Func<ToolPreset, ToolPreset> presetChange, string undoName, bool singleOnly = false)
    {
        if (_loading) return;
        if (Mode == PropertiesMode.ToolDefaults)
        {
            var tool = _main.ActiveTool;
            _main.ToolDefaults.Set(tool, presetChange(_main.ToolDefaults.Get(tool)));
            return;
        }
        if (_doc == null) return;
        var targets = Targets.Where(t => t is { IsEditable: true, IsLocked: false }).ToList();
        if (singleOnly && targets.Count != 1) return;
        var changes = targets.Select(t => (Item: t, Draft: change(t.Draft!))).Where(c => c.Draft != c.Item.Draft).ToList();
        if (changes.Count > 0) PendingOperation = _doc.ReplaceMarkupsAsync(changes, undoName);
    }

    private void Remember(RgbColor c)
    {
        RecentColors.Remove(c);
        RecentColors.Insert(0, c);
        while (RecentColors.Count > 5) RecentColors.RemoveAt(RecentColors.Count - 1);
    }
}
