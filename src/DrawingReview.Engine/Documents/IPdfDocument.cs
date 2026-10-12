using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.Engine.Documents;

/// <summary>
/// PDF 엔진 교체 경계. UI와 검토 기능은 이 인터페이스만 사용하고,
/// MuPDF 같은 특정 엔진의 타입은 이 경계 밖으로 나오지 않는다.
/// </summary>
public interface IPdfEngine
{
    string Name { get; }

    /// <summary>파일을 메모리로 읽어서 연다. 원본 파일은 잠그지 않으므로 같은 경로에 안전 저장할 수 있다.</summary>
    IPdfDocument Open(string path);
}

public interface IPdfDocument : IDisposable
{
    string? SourcePath { get; }
    int PageCount { get; }
    DocumentSecurityInfo Security { get; }

    /// <summary>암호가 걸린 문서를 연다. 성공하면 true.</summary>
    bool Authenticate(string password);

    PageInfo GetPageInfo(int pageIndex);

    /// <summary>페이지를 PNG로 렌더링한다. zoom 1.0 = 72 dpi. clip은 보이는 페이지 좌표(선택).</summary>
    byte[] RenderPagePng(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true);

    /// <summary>화면 표시용 렌더링 (RGB 원시 픽셀). clip을 주면 그 영역만 그린다.</summary>
    RenderedImage RenderPage(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true);

    /// <summary>표준 PDF 주석을 추가하고 고유 ID를 돌려준다. id를 주면 그 ID를 쓴다 (되돌리기로 다시 만들 때).</summary>
    string AddMarkup(int pageIndex, MarkupDraft draft, string? id = null);

    /// <summary>같은 ID로 주석을 다시 만든다 (이동·크기·속성 변경). 검토 상태·잠금·작성일은 유지한다. 없으면 false.</summary>
    bool ReplaceMarkup(int pageIndex, string markupId, MarkupDraft draft);

    /// <summary>검토 상태를 바꾼다 (null = 지움). 없으면 false.</summary>
    bool SetMarkupStatus(int pageIndex, string markupId, string? status);

    /// <summary>잠금을 켜거나 끈다. 없으면 false.</summary>
    bool SetMarkupLocked(int pageIndex, string markupId, bool locked);

    IReadOnlyList<MarkupInfo> GetMarkups(int pageIndex);

    /// <summary>모든 페이지의 주석. Review List의 원천 데이터.</summary>
    IReadOnlyList<MarkupInfo> GetAllMarkups();

    /// <summary>ID로 주석을 삭제한다. 없으면 false.</summary>
    bool DeleteMarkup(int pageIndex, string markupId);

    /// <summary>페이지의 모든 단어와 위치 (도면 비교의 글자 비교에 쓴다).</summary>
    IReadOnlyList<TextWord> GetWords(int pageIndex);

    /// <summary>페이지에서 글자를 찾아 일치 영역들을 돌려준다.</summary>
    IReadOnlyList<PageRect> SearchText(int pageIndex, string needle);

    /// <summary>
    /// 임시 파일에 쓰고 다시 열어 검증한 뒤 대상 파일을 교체한다.
    /// 실패하면 대상 파일은 저장 전 상태 그대로 남는다.
    /// </summary>
    void SaveTo(string path);

    /// <summary>
    /// 평탄화 사본: 주석 모양을 페이지 내용에 그려 넣은 별도 파일을 만든다. 열린 문서와 원본은 바뀌지 않는다.
    /// 평탄화만으로 편집 방지나 기밀 삭제가 보장되지는 않는다 (기획서 10장).
    /// </summary>
    void ExportFlattened(string path);
}

public sealed record DocumentSecurityInfo(bool IsEncrypted, bool NeedsPassword, bool HasSignatures)
{
    /// <summary>사용자에게 보여줄 경고 문장 목록.</summary>
    public IReadOnlyList<string> Warnings()
    {
        var list = new List<string>();
        if (NeedsPassword) list.Add("This PDF is password protected.");
        else if (IsEncrypted) list.Add("This PDF is encrypted; saving may remove or change its restrictions.");
        if (HasSignatures) list.Add("This PDF contains digital signatures; saving changes can invalidate them.");
        return list;
    }
}
