using DrawingReview.App.Core.Services;
using DrawingReview.TestData;

namespace DrawingReview.App.Core.Tests;

/// <summary>대화상자 대신 정해진 답을 돌려주고, 호출 기록을 남기는 가짜 구현.</summary>
public sealed class FakeDialogs : IDialogService
{
    public Queue<string> FilesToOpen { get; } = new();
    public Queue<string?> Passwords { get; } = new();
    public Queue<bool?> ConfirmAnswers { get; } = new();
    public string? SavePath { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Infos { get; } = [];
    public List<string> Confirms { get; } = [];

    public string? PickPdfFile(string title) => FilesToOpen.Count > 0 ? FilesToOpen.Dequeue() : null;
    public string? PickCadFile(string title) => FilesToOpen.Count > 0 ? FilesToOpen.Dequeue() : null;
    public string? PickCompareProject() => FilesToOpen.Count > 0 ? FilesToOpen.Dequeue() : null;
    public IReadOnlyList<string> PickPdfFilesToOpen() => FilesToOpen.Count > 0 ? [FilesToOpen.Dequeue()] : [];
    public string? PickSavePath(string suggestedFileName, string title = "Save PDF") => SavePath;
    public string? PromptPassword(string fileName, bool retry) => Passwords.Count > 0 ? Passwords.Dequeue() : null;
    public bool? Confirm(string title, string message, bool allowCancel = true)
    {
        Confirms.Add(message);
        return ConfirmAnswers.Count > 0 ? ConfirmAnswers.Dequeue() : null;
    }
    public void ShowError(string title, string message) => Errors.Add(title + ": " + message);
    public void ShowInfo(string title, string message) => Infos.Add(title + ": " + message);
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "drapp-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>가상 도면을 파일로 써 두고 경로를 돌려준다.</summary>
    public string WritePlanSet(string name = "plan-set.pdf")
    {
        var path = File(name);
        System.IO.File.WriteAllBytes(path, VirtualDrawings.CreatePlanSet());
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}
