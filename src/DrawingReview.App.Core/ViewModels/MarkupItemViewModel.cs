using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>
/// 마크업 하나 (Review List 행, 캔버스 선택 대상, 속성 패널 대상이 모두 이 객체를 공유한다).
/// </summary>
public sealed partial class MarkupItemViewModel : ObservableObject
{
    public MarkupItemViewModel(MarkupInfo info, string? pageLabel = null)
    {
        _info = info;
        PageLabel = pageLabel;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kind), nameof(KindName), nameof(Subject), nameof(Comment), nameof(Author),
        nameof(Status), nameof(Modified), nameof(ModifiedSortKey), nameof(IsLocked), nameof(IsEditable), nameof(Draft), nameof(Bounds), nameof(Page))]
    private MarkupInfo _info;

    public string Id => Info.Id;
    public int PageIndex => Info.PageIndex;
    public string? PageLabel { get; }

    /// <summary>1부터 시작하는 페이지 번호 (+라벨).</summary>
    public string Page => PageLabel is { } l && l != (PageIndex + 1).ToString() ? $"{PageIndex + 1} ({l})" : (PageIndex + 1).ToString();

    public MarkupKind Kind => Info.Kind;
    public string KindName => KindDisplayName(Info.Kind, Info.PdfSubtype);
    public string Subject => Info.Subject ?? "";
    public string Comment => Info.Comment ?? "";
    public string Author => Info.Author ?? "";

    /// <summary>상태가 기록되지 않은 마크업은 Open으로 보여준다 (다른 프로그램이 만든 주석 포함).</summary>
    public string Status => Info.Status ?? ReviewStatus.Open;

    public bool IsLocked => Info.IsLocked;
    public bool IsEditable => Info.IsEditable;
    public MarkupDraft? Draft => Info.Draft;
    public PageRect Bounds => Info.Bounds;

    public string Modified => ParsePdfDate(Info.ModifiedDate ?? Info.CreatedDate) is { } d ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";
    public DateTimeOffset ModifiedSortKey => ParsePdfDate(Info.ModifiedDate ?? Info.CreatedDate) ?? DateTimeOffset.MinValue;

    public static string KindDisplayName(MarkupKind kind, string pdfSubtype = "") => kind switch
    {
        MarkupKind.TextBox => "Text Box",
        MarkupKind.Callout => "Callout",
        MarkupKind.Line => "Line",
        MarkupKind.Arrow => "Arrow",
        MarkupKind.Rectangle => "Rectangle",
        MarkupKind.Ellipse => "Ellipse",
        MarkupKind.Cloud => "Cloud",
        MarkupKind.Polygon => "Polygon",
        MarkupKind.Ink => "Pen",
        MarkupKind.Highlight => "Highlight",
        _ => string.IsNullOrEmpty(pdfSubtype) ? "Other" : pdfSubtype,
    };

    /// <summary>PDF 날짜 문자열 (D:YYYYMMDDHHmmSS+HH'mm') → 날짜. 읽을 수 없으면 null.</summary>
    public static DateTimeOffset? ParsePdfDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var t = s.StartsWith("D:", StringComparison.Ordinal) ? s[2..] : s;
        if (t.Length < 8) return null;
        try
        {
            int year = int.Parse(t[..4], CultureInfo.InvariantCulture);
            int Part(int start, int fallback) => t.Length >= start + 2 ? int.Parse(t.Substring(start, 2), CultureInfo.InvariantCulture) : fallback;
            int month = Part(4, 1), day = Part(6, 1), hour = Part(8, 0), minute = Part(10, 0), second = Part(12, 0);
            var offset = TimeSpan.Zero;
            if (t.Length > 14 && (t[14] == '+' || t[14] == '-') && t.Length >= 17)
            {
                var oh = int.Parse(t.Substring(15, 2), CultureInfo.InvariantCulture);
                var om = t.Length >= 20 ? int.Parse(t.Substring(18, 2), CultureInfo.InvariantCulture) : 0;
                offset = new TimeSpan(oh, om, 0) * (t[14] == '-' ? -1 : 1);
            }
            return new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
