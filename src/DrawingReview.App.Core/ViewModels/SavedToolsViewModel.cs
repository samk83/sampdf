using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>
/// 저장한 도구 하나 (기획서 7장 Saved Tools).
/// IncludeContent가 false면 "스타일만": 같은 도구로 새로 그린다.
/// true면 "문구·모양까지": 저장한 모양(Template)을 클릭한 위치에 그대로 놓는다.
/// </summary>
public sealed record SavedTool(string Name, ToolKind Tool, MarkupStyle Style, string Subject, string? Text = null, bool IncludeContent = false, MarkupDraftData? Template = null)
{
    public string Description => IncludeContent ? (Text is { Length: > 0 } t ? $"\"{t}\"" : "Shape and text") : $"{ToolInfo.DisplayName(Tool)} style";
}

/// <summary>JSON 저장을 위한 모양 데이터 (MarkupDraft는 추상 형식이라 직접 저장하지 않고 이 형태로 바꾼다).</summary>
public sealed record MarkupDraftData(string Json)
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = false };

    public static MarkupDraftData From(MarkupDraft d) => new(d switch
    {
        TextBoxDraft t => "TextBox:" + JsonSerializer.Serialize(t, Options),
        CalloutDraft c => "Callout:" + JsonSerializer.Serialize(c, Options),
        LineDraft l => "Line:" + JsonSerializer.Serialize(l, Options),
        RectangleDraft r => "Rectangle:" + JsonSerializer.Serialize(r, Options),
        EllipseDraft e => "Ellipse:" + JsonSerializer.Serialize(e, Options),
        CloudDraft c => "Cloud:" + JsonSerializer.Serialize(c, Options),
        InkDraft i => "Ink:" + JsonSerializer.Serialize(i, Options),
        HighlightDraft h => "Highlight:" + JsonSerializer.Serialize(h, Options),
        _ => throw new NotSupportedException(d.GetType().Name),
    });

    public MarkupDraft ToDraft()
    {
        var i = Json.IndexOf(':');
        var (kind, body) = (Json[..i], Json[(i + 1)..]);
        return kind switch
        {
            "TextBox" => JsonSerializer.Deserialize<TextBoxDraft>(body, Options)!,
            "Callout" => JsonSerializer.Deserialize<CalloutDraft>(body, Options)!,
            "Line" => JsonSerializer.Deserialize<LineDraft>(body, Options)!,
            "Rectangle" => JsonSerializer.Deserialize<RectangleDraft>(body, Options)!,
            "Ellipse" => JsonSerializer.Deserialize<EllipseDraft>(body, Options)!,
            "Cloud" => JsonSerializer.Deserialize<CloudDraft>(body, Options)!,
            "Ink" => JsonSerializer.Deserialize<InkDraft>(body, Options)!,
            "Highlight" => JsonSerializer.Deserialize<HighlightDraft>(body, Options)!,
            _ => throw new NotSupportedException(kind),
        };
    }
}

public sealed class SavedToolSet
{
    public string Name { get; set; } = "";
    public List<SavedTool> Tools { get; set; } = [];
}

/// <summary>왼쪽 Saved Tools 패널.</summary>
public sealed partial class SavedToolsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public SavedToolsViewModel(MainViewModel main)
    {
        _main = main;
        foreach (var set in DefaultSets()) Sets.Add(set);
        SelectedSet = Sets[0];
    }

    public ObservableCollection<SavedToolSet> Sets { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tools))]
    private SavedToolSet? _selectedSet;

    public IReadOnlyList<SavedTool> Tools => SelectedSet?.Tools ?? [];

    [ObservableProperty] private SavedTool? _activeSavedTool;

    /// <summary>기획서 7장의 예시 세트: Structural Review, Coordination, Site Notes.</summary>
    public static List<SavedToolSet> DefaultSets()
    {
        var red = MarkupStyle.Default with { StrokeColor = new RgbColor(0.898, 0.216, 0.173), TextColor = new RgbColor(0.898, 0.216, 0.173) };
        var blue = MarkupStyle.Default with { StrokeColor = new RgbColor(0.184, 0.435, 0.871), TextColor = new RgbColor(0.184, 0.435, 0.871) };
        return
        [
            new SavedToolSet
            {
                Name = "Structural Review",
                Tools =
                [
                    new("Revision cloud", ToolKind.Cloud, red with { LineWidth = 2 }, "Structural"),
                    new("RFI callout", ToolKind.Callout, red, "RFI", "RFI: ", IncludeContent: true),
                    new("Verify clearance", ToolKind.Callout, red, "Structural", "VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM.", IncludeContent: true),
                ],
            },
            new SavedToolSet
            {
                Name = "Coordination",
                Tools =
                [
                    new("Coordination highlight", ToolKind.Highlight, MarkupStyle.Highlighter, "Coordination"),
                    new("Clash arrow", ToolKind.Arrow, blue with { LineWidth = 2 }, "Coordination"),
                    new("Coordinate with MEP", ToolKind.TextBox, blue, "Coordination", "COORDINATE WITH MEP", IncludeContent: true),
                ],
            },
            new SavedToolSet
            {
                Name = "Site Notes",
                Tools =
                [
                    new("Field verify", ToolKind.TextBox, blue, "Site Notes", "FIELD VERIFY", IncludeContent: true),
                    new("Site pen", ToolKind.Pen, blue with { LineWidth = 2 }, "Site Notes"),
                ],
            },
        ];
    }

    /// <summary>
    /// 저장 도구 사용: 해당 그리기 도구에 스타일·주제·문구를 적용하고 도구를 켠다.
    /// 모양까지 저장한 도구는 다음 클릭에 그 모양을 놓는다.
    /// </summary>
    [RelayCommand]
    public void Use(SavedTool tool)
    {
        _main.ToolDefaults.Set(tool.Tool, new ToolPreset(tool.Style, tool.Subject, tool.IncludeContent ? tool.Text : null));
        ActiveSavedTool = tool;
        if (tool.Template != null && _main.Interaction is { } ui)
        {
            ui.BeginPlacement(tool.Template.ToDraft() with { Style = tool.Style, Subject = tool.Subject, Author = _main.ToolDefaults.Author });
            _main.ActiveTool = ToolKind.Select;
            _main.StatusMessage = $"Click on the drawing to place \"{tool.Name}\" (Esc to cancel)";
        }
        else
        {
            _main.ActiveTool = tool.Tool;
            _main.StatusMessage = $"Tool: {tool.Name}";
        }
    }

    /// <summary>선택한 마크업을 저장 도구로 만든다. includeContent: 문구·모양까지 저장.</summary>
    public SavedTool? SaveSelected(string name, bool includeContent)
    {
        if (_main.ActiveDocument?.SelectedMarkup is not { Draft: { } draft } || SelectedSet == null) return null;
        var text = draft switch { TextBoxDraft t => t.Text, CalloutDraft c => c.Text, _ => null };
        var tool = new SavedTool(
            string.IsNullOrWhiteSpace(name) ? ToolInfo.DisplayName(ToolDefaults.ToolFor(draft)) : name.Trim(),
            ToolDefaults.ToolFor(draft), draft.Style, draft.Subject ?? "", text, includeContent,
            includeContent && draft is not (TextBoxDraft or CalloutDraft) ? MarkupDraftData.From(Normalize(draft)) : null);
        SelectedSet.Tools.Add(tool);
        OnPropertyChanged(nameof(Tools));
        return tool;
    }

    /// <summary>모양의 왼쪽 위가 (0,0)이 되도록 옮겨 저장한다 (놓을 때 클릭 위치 기준).</summary>
    private static MarkupDraft Normalize(MarkupDraft d)
    {
        var b = DraftGeometry.Bounds(d);
        return DraftGeometry.Translate(d, -b.X0, -b.Y0);
    }

    [RelayCommand]
    private void Delete(SavedTool tool)
    {
        if (SelectedSet?.Tools.Remove(tool) == true) OnPropertyChanged(nameof(Tools));
    }

    [RelayCommand]
    private void AddSet()
    {
        var set = new SavedToolSet { Name = $"My Tools {Sets.Count + 1}" };
        Sets.Add(set);
        SelectedSet = set;
    }

    public void Load(IReadOnlyList<SavedToolSet>? sets)
    {
        if (sets == null || sets.Count == 0) return;
        Sets.Clear();
        foreach (var s in sets) Sets.Add(s);
        SelectedSet = Sets[0];
    }

    /// <summary>세트를 파일로 내보내기·가져오기 (자체 JSON 형식 — Bluebeam 도구 세트 직접 호환은 범위 밖, 기획서 7장).</summary>
    public void ExportSet(string path)
    {
        if (SelectedSet == null) return;
        File.WriteAllText(path, JsonSerializer.Serialize(SelectedSet, SettingsStore.Json));
    }

    public SavedToolSet ImportSet(string path)
    {
        var set = JsonSerializer.Deserialize<SavedToolSet>(File.ReadAllText(path), SettingsStore.Json)
                  ?? throw new InvalidDataException("The tool set file is empty.");
        Sets.Add(set);
        SelectedSet = set;
        return set;
    }
}
