using System.Text.Json;
using System.Text.Json.Serialization;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Persistence;

namespace DrawingReview.App.Core.Comparison;

/// <summary>
/// 비교 프로젝트 파일 (.drcompare, JSON): 두 도면 경로, 페이지 짝, 설정, 정렬, 변경 상태.
/// 다시 열면 같은 결과와 검토 상태를 그대로 보여준다 (기획서 9장 Other).
/// </summary>
public sealed class CompareProject
{
    public const string Extension = ".drcompare";

    public int Version { get; set; } = 1;
    public string OldPath { get; set; } = "";
    public string NewPath { get; set; } = "";
    public List<PageEntry> Pages { get; set; } = [];
    public CloudOptions Clouds { get; set; } = new();
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;

    public sealed class PageEntry
    {
        public int OldPage { get; set; }
        public int NewPage { get; set; }
        public CompareSettings Settings { get; set; } = new();
        public AlignmentResult? Alignment { get; set; }
        public List<ChangeRegion> Changes { get; set; } = [];
        /// <summary>변경 번호 → 만든 Revision Cloud 마크업 ID.</summary>
        public Dictionary<int, string> Clouds { get; set; } = new();
        public List<int> Hidden { get; set; } = [];
    }

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Save(string path)
    {
        SavedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(this, Json);
        SafeFileSaver.Save(path, t => File.WriteAllText(t, json), t => JsonSerializer.Deserialize<CompareProject>(File.ReadAllText(t), Json));
    }

    public static CompareProject Load(string path) =>
        JsonSerializer.Deserialize<CompareProject>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException("The comparison project file is empty.");
}
