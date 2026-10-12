using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.Editing;

/// <summary>도구 하나의 기본 설정: 스타일, 주제(Subject), 미리 넣을 문구.</summary>
public sealed record ToolPreset(MarkupStyle Style, string Subject, string? DefaultText = null);

/// <summary>
/// 도구별 기본값과 작성자 이름. 새 마크업은 여기서 스타일·작성자·주제를 받는다.
/// Saved Tools를 고르면 해당 도구의 프리셋이 잠시 이것을 대신한다.
/// </summary>
public sealed class ToolDefaults
{
    private readonly Dictionary<ToolKind, ToolPreset> _presets = new();

    public ToolDefaults(string author)
    {
        Author = author;
        var red = MarkupStyle.Default;
        foreach (var tool in Enum.GetValues<ToolKind>())
            _presets[tool] = new ToolPreset(red, ToolInfo.DisplayName(tool));
        _presets[ToolKind.Highlight] = new ToolPreset(MarkupStyle.Highlighter, "Highlight");
        _presets[ToolKind.Cloud] = new ToolPreset(red with { LineWidth = 2 }, "Cloud");
        _presets[ToolKind.Pen] = new ToolPreset(red with { LineWidth = 2 }, "Pen");
    }

    public string Author { get; set; }

    public event EventHandler<ToolKind>? Changed;

    public ToolPreset Get(ToolKind tool) => _presets[tool];

    public void Set(ToolKind tool, ToolPreset preset)
    {
        _presets[tool] = preset;
        Changed?.Invoke(this, tool);
    }

    public void SetStyle(ToolKind tool, MarkupStyle style) => Set(tool, Get(tool) with { Style = style });

    /// <summary>새로 그린 도형에 도구 기본값(스타일·작성자·주제)을 입힌다.</summary>
    public MarkupDraft Decorate(ToolKind tool, MarkupDraft draft)
    {
        var p = Get(tool);
        return draft with { Style = p.Style, Author = Author, Subject = p.Subject };
    }

    /// <summary>마크업 종류에 맞는 도구 (선택한 마크업을 Saved Tool로 저장할 때).</summary>
    public static ToolKind ToolFor(MarkupDraft draft) => draft switch
    {
        TextBoxDraft => ToolKind.TextBox,
        CalloutDraft => ToolKind.Callout,
        LineDraft { ArrowAtEnd: true } => ToolKind.Arrow,
        LineDraft => ToolKind.Line,
        RectangleDraft => ToolKind.Rectangle,
        EllipseDraft => ToolKind.Ellipse,
        CloudDraft => ToolKind.Cloud,
        InkDraft => ToolKind.Pen,
        HighlightDraft => ToolKind.Highlight,
        _ => ToolKind.Select,
    };
}
