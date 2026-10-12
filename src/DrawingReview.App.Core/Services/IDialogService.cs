namespace DrawingReview.App.Core.Services;

/// <summary>
/// 파일 선택·확인 창 같은 화면 기능. ViewModel은 이 인터페이스만 알고,
/// 실제 창은 WPF 프로젝트가 구현한다 (테스트에서는 가짜 구현을 넣는다).
/// </summary>
public interface IDialogService
{
    /// <summary>열 PDF 파일들을 고른다. 취소하면 빈 목록.</summary>
    IReadOnlyList<string> PickPdfFilesToOpen();

    /// <summary>PDF 하나를 고른다 (제목으로 무엇을 고르는지 알려 준다). 취소하면 null.</summary>
    string? PickPdfFile(string title);

    /// <summary>DWG 또는 DXF 하나를 고른다. 취소하면 null.</summary>
    string? PickCadFile(string title);

    /// <summary>비교 프로젝트 파일을 고른다. 취소하면 null.</summary>
    string? PickCompareProject();

    /// <summary>저장할 경로를 고른다. 취소하면 null.</summary>
    string? PickSavePath(string suggestedFileName, string title = "Save PDF");

    /// <summary>암호를 묻는다. 취소하면 null.</summary>
    string? PromptPassword(string fileName, bool retry);

    /// <summary>예/아니오/취소. 취소면 null.</summary>
    bool? Confirm(string title, string message, bool allowCancel = true);

    void ShowError(string title, string message);

    void ShowInfo(string title, string message);
}
