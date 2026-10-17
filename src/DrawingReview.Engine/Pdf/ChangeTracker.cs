using System.Runtime.CompilerServices;
using PdfSharp.Pdf;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 문서 안에서 새로 만들거나 바꾼 간접 객체를 기억한다. <see cref="IncrementalWriter"/>가 이 객체들만 파일 끝에 덧붙인다.
/// PDFsharp 문서는 Save를 한 번만 할 수 있어서, 편집할 때마다 전체를 다시 쓰는 대신 바뀐 부분만 쓰는 방식을 택했다.
/// </summary>
internal sealed class ChangeTracker
{
    private static readonly ConditionalWeakTable<PdfDocument, ChangeTracker> Trackers = new();

    private readonly HashSet<PdfObject> _changed = new(ReferenceEqualityComparer.Instance);

    public static ChangeTracker For(PdfDocument doc) => Trackers.GetValue(doc, _ => new ChangeTracker());

    /// <summary>바뀐 객체 수.</summary>
    public int Count => _changed.Count;

    public void Touch(PdfObject obj)
    {
        if (obj.Reference == null) throw new InvalidOperationException("Only indirect objects can be tracked.");
        _changed.Add(obj);
    }

    /// <summary>지금까지 모은 객체를 돌려주고 비운다.</summary>
    public List<PdfObject> Take()
    {
        var list = _changed.ToList();
        _changed.Clear();
        return list;
    }
}
