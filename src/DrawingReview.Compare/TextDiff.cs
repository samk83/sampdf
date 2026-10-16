using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.Compare;

/// <summary>
/// 단어 단위 비교. 이전 도면 단어를 정렬 변환으로 옮긴 뒤, 같은 글자가 가까이(글자 높이의 절반 + 2pt) 있으면 짝이 맞는다고 본다.
/// 짝이 없는 개정 도면 단어 = 추가, 짝이 없는 이전 도면 단어 = 삭제 (위치는 개정 도면 좌표).
/// </summary>
public static class TextDiff
{
    /// <returns>추가된 단어(개정 도면 좌표)와 삭제된 단어(이전 도면 단어를 개정 도면 좌표로 옮긴 것).</returns>
    public static (List<TextWord> Added, List<TextWord> Removed) Compare(
        IReadOnlyList<TextWord> oldWords, IReadOnlyList<TextWord> newWords, AlignmentTransform transform)
    {
        var movedOld = oldWords.Select(w => w with { Bounds = transform.Apply(w.Bounds) }).ToList();
        var usedOld = new bool[movedOld.Count];
        var added = new List<TextWord>();
        foreach (var w in newWords)
        {
            var tol = w.Bounds.Height / 2 + 2;
            var match = -1;
            var best = double.MaxValue;
            for (int i = 0; i < movedOld.Count; i++)
            {
                if (usedOld[i] || movedOld[i].Text != w.Text) continue;
                var d = Distance(movedOld[i].Bounds.Center, w.Bounds.Center);
                if (d <= tol && d < best) { best = d; match = i; }
            }
            if (match >= 0) usedOld[match] = true;
            else added.Add(w);
        }
        var removed = movedOld.Where((_, i) => !usedOld[i]).ToList();
        return (added, removed);
    }

    private static double Distance(PagePoint a, PagePoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
