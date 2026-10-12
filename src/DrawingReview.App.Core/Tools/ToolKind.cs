namespace DrawingReview.App.Core.Tools;

/// <summary>도구줄의 도구. 단축키는 Bluebeam과 겹치지 않아도 되는 자체 기본안이다.</summary>
public enum ToolKind
{
    Select,
    Pan,
    Pen,
    Highlight,
    TextBox,
    Callout,
    Cloud,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
}

public static class ToolInfo
{
    public static string DisplayName(ToolKind tool) => tool switch
    {
        ToolKind.Select => "Select",
        ToolKind.Pan => "Pan",
        ToolKind.Pen => "Pen",
        ToolKind.Highlight => "Highlight",
        ToolKind.TextBox => "Text Box",
        ToolKind.Callout => "Callout",
        ToolKind.Cloud => "Cloud",
        ToolKind.Line => "Line",
        ToolKind.Arrow => "Arrow",
        ToolKind.Rectangle => "Rectangle",
        ToolKind.Ellipse => "Ellipse",
        _ => tool.ToString(),
    };

    /// <summary>단축키 (Ctrl 없이 누르는 글자).</summary>
    public static char Shortcut(ToolKind tool) => tool switch
    {
        ToolKind.Select => 'V',
        ToolKind.Pan => 'H',
        ToolKind.Pen => 'P',
        ToolKind.Highlight => 'I',
        ToolKind.TextBox => 'T',
        ToolKind.Callout => 'Q',
        ToolKind.Cloud => 'C',
        ToolKind.Line => 'L',
        ToolKind.Arrow => 'A',
        ToolKind.Rectangle => 'R',
        ToolKind.Ellipse => 'E',
        _ => ' ',
    };

    public static ToolKind? FromShortcut(char key)
    {
        foreach (var t in Enum.GetValues<ToolKind>())
            if (char.ToUpperInvariant(key) == Shortcut(t)) return t;
        return null;
    }

    /// <summary>마크업을 만드는 도구인지 (선택·이동 도구가 아닌지).</summary>
    public static bool CreatesMarkup(ToolKind tool) => tool is not (ToolKind.Select or ToolKind.Pan);
}
