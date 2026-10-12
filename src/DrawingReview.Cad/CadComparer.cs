namespace DrawingReview.Cad;

/// <summary>
/// CAD 도면 비교 (기획서 7장 CAD 규칙):
/// - 객체 ID(handle)를 쓰지 않는다. 같은 종류·같은 위치·같은 모양이면 같은 객체로 본다.
/// - 짝이 없는 이전 요소와 개정 요소 중
///   · 모양이 같고 위치만 다르면 → 이동(Moved)
///   · 같은 종류가 같은 자리 근처에서 모양·글자만 다르면 → 변경(Modified)
///   · 나머지는 추가(Added) / 삭제(Removed).
/// 좌표는 mm, 허용 오차 기본 0.5mm.
/// </summary>
public static class CadComparer
{
    public static CadCompareResult Compare(CadDrawing oldDrawing, CadDrawing newDrawing, double toleranceMm = 0.5, double moveSearchMm = 5000)
    {
        var warnings = oldDrawing.Warnings.Select(w => "Old: " + w).Concat(newDrawing.Warnings.Select(w => "New: " + w)).ToList();
        if (Math.Abs(oldDrawing.UnitToMm - newDrawing.UnitToMm) > 1e-9)
            warnings.Add($"Units differ (old {oldDrawing.UnitsName}, new {newDrawing.UnitsName}); both were converted to millimeters.");

        var oldList = oldDrawing.Primitives.ToList();
        var newList = newDrawing.Primitives.ToList();
        var oldUsed = new bool[oldList.Count];
        var newUsed = new bool[newList.Count];

        // 1) 같은 객체 짝 맞추기
        //    빠른 길: 좌표를 허용 오차 격자로 반올림한 열쇠가 같으면 바로 짝 (대부분의 요소)
        //    느린 길: 격자 경계에 걸린 것만 공간 색인으로 근처 후보를 찾아 비교
        var byKey = new Dictionary<string, Queue<int>>();
        for (int j = 0; j < oldList.Count; j++)
        {
            var key = Key(oldList[j], toleranceMm);
            if (!byKey.TryGetValue(key, out var q)) byKey[key] = q = new Queue<int>();
            q.Enqueue(j);
        }
        for (int i = 0; i < newList.Count; i++)
        {
            if (!byKey.TryGetValue(Key(newList[i], toleranceMm), out var q)) continue;
            while (q.Count > 0)
            {
                var j = q.Dequeue();
                if (oldUsed[j] || !Same(oldList[j], newList[i], toleranceMm)) continue;
                oldUsed[j] = newUsed[i] = true;
                break;
            }
        }
        var index = new SpatialIndex(oldList.Select((p, j) => oldUsed[j] ? null : p).ToList(), Math.Max(toleranceMm * 20, 50));
        for (int i = 0; i < newList.Count; i++)
        {
            if (newUsed[i]) continue;
            var n = newList[i];
            foreach (var j in index.Near(n.Bounds.Inflate(toleranceMm * 2)))
            {
                if (oldUsed[j] || !Same(oldList[j], n, toleranceMm)) continue;
                oldUsed[j] = newUsed[i] = true;
                break;
            }
        }

        var remainingOld = Enumerable.Range(0, oldList.Count).Where(j => !oldUsed[j]).ToList();
        var remainingNew = Enumerable.Range(0, newList.Count).Where(i => !newUsed[i]).ToList();
        var changes = new List<CadChange>();

        // 2) 이동: 모양 특징이 같고 위치만 다른 짝 (가장 가까운 것)
        foreach (var i in remainingNew.ToList())
        {
            var n = newList[i];
            var sig = n.ShapeSignature;
            var best = -1;
            var bestDist = double.MaxValue;
            foreach (var j in remainingOld)
            {
                var o = oldList[j];
                if (o.Type != n.Type || o.Layer != n.Layer || o.ShapeSignature != sig) continue;
                var d = o.Bounds.Center.DistanceTo(n.Bounds.Center);
                if (d < bestDist && d <= moveSearchMm) { bestDist = d; best = j; }
            }
            if (best < 0) continue;
            var old = oldList[best];
            changes.Add(new CadChange(CadChangeKind.Moved, n.Type, n.Layer, old.Bounds.Union(n.Bounds),
                $"{n.Type} moved {bestDist:0.#} mm" + (n.Type == PrimitiveType.Text ? $" (\"{n.Text}\")" : "")));
            remainingOld.Remove(best);
            remainingNew.Remove(i);
        }

        // 3) 변경: 같은 종류가 같은 자리 근처에 있으나 모양·글자가 다름
        foreach (var i in remainingNew.ToList())
        {
            var n = newList[i];
            var near = remainingOld.Where(j => oldList[j].Type == n.Type && Anchor(oldList[j]).DistanceTo(Anchor(n)) <= Math.Max(toleranceMm * 4, NearDistance(n)))
                .OrderBy(j => Anchor(oldList[j]).DistanceTo(Anchor(n))).ToList();
            if (near.Count == 0) continue;
            var o = oldList[near[0]];
            changes.Add(new CadChange(CadChangeKind.Modified, n.Type, n.Layer, o.Bounds.Union(n.Bounds), Describe(o, n)));
            remainingOld.Remove(near[0]);
            remainingNew.Remove(i);
        }

        foreach (var i in remainingNew)
            changes.Add(new CadChange(CadChangeKind.Added, newList[i].Type, newList[i].Layer, newList[i].Bounds, $"{newList[i].Type} added" + TextSuffix(newList[i])));
        foreach (var j in remainingOld)
            changes.Add(new CadChange(CadChangeKind.Removed, oldList[j].Type, oldList[j].Layer, oldList[j].Bounds, $"{oldList[j].Type} removed" + TextSuffix(oldList[j])));

        var ordered = changes.OrderByDescending(c => c.Bounds.MaxY).ThenBy(c => c.Bounds.MinX).ToList(); // CAD는 y가 위로 증가: 위→아래
        return new CadCompareResult(ordered, oldList.Count, newList.Count, warnings);
    }

    /// <summary>허용 오차 격자로 반올림한 비교 열쇠.</summary>
    private static string Key(CadPrimitive p, double tol)
    {
        var g = Math.Max(tol, 1e-6);
        var sb = new System.Text.StringBuilder();
        sb.Append((int)p.Type).Append('|').Append(p.Layer).Append('|');
        foreach (var pt in p.Points) sb.Append(Math.Round(pt.X / g)).Append(',').Append(Math.Round(pt.Y / g)).Append(';');
        sb.Append(Math.Round(p.Size / g)).Append('|').Append(p.Type == PrimitiveType.Dimension && !p.IsOverriddenDimension ? "" : p.Text).Append('|').Append(Math.Round(p.A1, 1)).Append('|').Append(p.Type == PrimitiveType.Dimension ? 0 : Math.Round(p.A2, 1)).Append(p.Closed);
        return sb.ToString();
    }

    private static string TextSuffix(CadPrimitive p) => p.Type is PrimitiveType.Text or PrimitiveType.Dimension ? $" (\"{p.Text}\")" : "";

    private static string Describe(CadPrimitive o, CadPrimitive n) => n.Type switch
    {
        PrimitiveType.Text => $"Text changed \"{o.Text}\" → \"{n.Text}\"",
        PrimitiveType.Circle => $"Circle radius {o.Size:0.##} → {n.Size:0.##} mm",
        PrimitiveType.Line => $"Line changed (length {o.Points[0].DistanceTo(o.Points[1]):0.#} → {n.Points[0].DistanceTo(n.Points[1]):0.#} mm)",
        PrimitiveType.Dimension => DescribeDimension(o, n),
        _ => $"{n.Type} changed",
    };

    /// <summary>
    /// 치수 변경 설명. 문자를 직접 고쳐 쓴(override) 치수는 측정값이 바뀌어도 문자가 그대로일 수 있어
    /// 그 경우를 따로 알린다 (보이는 숫자와 실제 길이가 다름).
    /// </summary>
    private static string DescribeDimension(CadPrimitive o, CadPrimitive n)
    {
        var measured = Math.Abs(o.Size - n.Size) > 1e-6 ? $"measured {o.Size:0.##} → {n.Size:0.##}" : "";
        if (o.Text != n.Text)
            return $"Dimension \"{o.Text}\" → \"{n.Text}\"" + (measured.Length > 0 && !n.Text.Contains(n.Size.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)) ? $" ({measured})" : "");
        if (measured.Length > 0)
            return $"Dimension {measured} but text is still \"{n.Text}\" — check text override";
        return "Dimension position changed";
    }

    /// <summary>"같은 자리"의 기준점: 선은 한쪽 끝, 원·호·글자는 중심·삽입점, 폴리선은 첫 점, 치수는 가운데.</summary>
    private static Pt Anchor(CadPrimitive p) => p.Type == PrimitiveType.Dimension ? p.Bounds.Center : p.Points[0];

    private static double NearDistance(CadPrimitive p) => p.Type switch
    {
        PrimitiveType.Dimension => Math.Max(p.Size * 0.5, 1),
        PrimitiveType.Text => Math.Max(p.Size * 2, 1),
        PrimitiveType.Line => 1,
        _ => Math.Max(p.Size * 0.5, 1),
    };

    /// <summary>같은 객체인지: 종류·도면층·좌표(허용 오차)·크기·글자가 같다.</summary>
    public static bool Same(CadPrimitive a, CadPrimitive b, double tol)
    {
        if (a.Type != b.Type || a.Layer != b.Layer || a.Points.Count != b.Points.Count) return false;
        for (int k = 0; k < a.Points.Count; k++)
            if (a.Points[k].DistanceTo(b.Points[k]) > tol) return false;
        if (Math.Abs(a.Size - b.Size) > tol) return false;
        if (a.Type == PrimitiveType.Arc && (AngleDiff(a.A1, b.A1) > 0.1 || AngleDiff(a.A2, b.A2) > 0.1)) return false;
        if (a.Type == PrimitiveType.Text && (a.Text != b.Text || AngleDiff(a.A1, b.A1) > 0.1)) return false;
        // 자동 문자 치수는 측정값(Size, mm)이 같으면 같다 (mm 도면 "6000"과 m 도면 "6"). 덮어쓴 문자는 글자로 비교한다.
        if (a.Type == PrimitiveType.Dimension && (a.IsOverriddenDimension || b.IsOverriddenDimension) && a.Text != b.Text) return false;
        return a.Closed == b.Closed;
    }

    private static double AngleDiff(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return Math.Min(d, 360 - d);
    }

    /// <summary>
    /// 단순 격자 공간 색인: 근처 후보만 비교해 큰 도면에서도 빠르게 한다.
    /// 칸을 너무 많이 차지하는 큰 요소(긴 선 등)는 따로 두고 항상 후보에 넣는다.
    /// </summary>
    private sealed class SpatialIndex
    {
        private const long MaxCells = 400;
        private readonly Dictionary<(long, long), List<int>> _cells = new();
        private readonly List<int> _large = [];
        private readonly double _cell;

        public SpatialIndex(IReadOnlyList<CadPrimitive?> items, double cellSize)
        {
            _cell = cellSize;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not { } item) continue; // 이미 짝이 맞은 요소는 색인하지 않음
                var (x0, y0, x1, y1) = Range(item.Bounds);
                if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxCells) { _large.Add(i); continue; }
                for (var x = x0; x <= x1; x++)
                    for (var y = y0; y <= y1; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out var list)) _cells[(x, y)] = list = [];
                        list.Add(i);
                    }
            }
        }

        public IEnumerable<int> Near(Box b)
        {
            var seen = new HashSet<int>();
            foreach (var i in _large)
                if (seen.Add(i)) yield return i;
            var (x0, y0, x1, y1) = Range(b);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxCells * 4)
            {
                // 아주 큰 질의: 모든 칸을 훑는다
                foreach (var list in _cells.Values)
                    foreach (var i in list)
                        if (seen.Add(i)) yield return i;
                yield break;
            }
            for (var x = x0; x <= x1; x++)
                for (var y = y0; y <= y1; y++)
                    if (_cells.TryGetValue((x, y), out var list))
                        foreach (var i in list)
                            if (seen.Add(i)) yield return i;
        }

        private (long, long, long, long) Range(Box b) =>
            ((long)Math.Floor(b.MinX / _cell), (long)Math.Floor(b.MinY / _cell), (long)Math.Floor(b.MaxX / _cell), (long)Math.Floor(b.MaxY / _cell));
    }
}
