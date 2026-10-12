using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.Editing;

[Flags]
public enum PointerModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
}

/// <summary>글상자·Callout을 그린 뒤(또는 더블클릭해 고칠 때) 화면이 글자 입력 칸을 띄우도록 하는 요청.</summary>
public sealed record TextEditRequest(int PageIndex, PageRect TextRect, string InitialText, MarkupDraft Draft, MarkupItemViewModel? Existing);

/// <summary>
/// 캔버스 마우스 동작 → 마크업 그리기·선택·이동·크기 조절.
/// 화면(WPF)은 마우스 위치를 페이지 좌표로 바꿔 넘기고, 이 클래스가 만든 미리보기를 그리기만 한다.
/// </summary>
public sealed class MarkupInteraction
{
    /// <summary>마우스로 잡을 수 있는 거리 (화면 픽셀).</summary>
    public const double HitPixels = 6;
    /// <summary>이보다 작게 끌면 클릭으로 본다 (화면 픽셀).</summary>
    public const double ClickPixels = 4;
    /// <summary>클릭만 했을 때 만드는 글상자 크기 (화면 픽셀).</summary>
    public const double DefaultTextWidthPixels = 240, DefaultTextHeightPixels = 56;

    private readonly DocumentViewModel _doc;
    private readonly ToolDefaults _defaults;

    private Mode _mode = Mode.Idle;
    private ToolKind _tool;
    private PagePoint _start;
    private PagePoint _last;
    private double _pixel = 1; // 화면 1픽셀 = 몇 pt
    private readonly List<PagePoint> _inkPoints = [];
    private List<MarkupItemViewModel> _moving = [];
    private MarkupItemViewModel? _resizing;
    private HandleKind _handle;

    private MarkupDraft? _placement;

    private enum Mode { Idle, Drawing, Moving, Resizing, RubberBand, Placing }

    /// <summary>저장 도구(모양까지 저장)를 놓을 준비: 다음 클릭 위치에 template을 놓는다 (template의 왼쪽 위 = 클릭 점).</summary>
    public void BeginPlacement(MarkupDraft template)
    {
        _placement = template;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsPlacing => _placement != null;

    public MarkupInteraction(DocumentViewModel doc, ToolDefaults defaults)
    {
        _doc = doc;
        _defaults = defaults;
    }

    /// <summary>그리는 중인 도형, 옮기는 중인 도형의 미리보기 (화면이 겹쳐 그린다).</summary>
    public IReadOnlyList<MarkupDraft> Previews { get; private set; } = [];

    /// <summary>끌어서 여러 개를 고르는 사각형.</summary>
    public PageRect? RubberBand { get; private set; }

    /// <summary>미리보기 동안 원래 그림을 흐리게 할 마크업 (옮기는 중인 것).</summary>
    public IReadOnlyCollection<string> HiddenIds => _mode is Mode.Moving or Mode.Resizing ? _moving.Select(m => m.Id).ToHashSet() : [];

    public bool IsBusy => _mode != Mode.Idle || _placement != null;

    /// <summary>미리보기가 바뀌었을 때 (화면 다시 그리기).</summary>
    public event EventHandler? Changed;

    /// <summary>글자 입력 칸이 필요할 때.</summary>
    public event EventHandler<TextEditRequest>? TextEditRequested;

    /// <summary>마크업을 하나 만들었을 때 (연속 배치가 꺼져 있으면 선택 도구로 돌아가는 데 쓴다).</summary>
    public event EventHandler<MarkupItemViewModel>? MarkupCreated;

    private int Page => _doc.CurrentPageIndex;

    /// <summary>
    /// 마우스를 눌렀을 때. pixelSize = 화면 1픽셀이 몇 pt인지 (1 / 확대율).
    /// </summary>
    public void PointerDown(PagePoint p, ToolKind tool, PointerModifiers mods, double pixelSize)
    {
        _tool = tool;
        _start = _last = p;
        _pixel = pixelSize;
        Previews = [];
        RubberBand = null;

        if (_placement != null)
        {
            _mode = Mode.Placing;
        }
        else if (tool == ToolKind.Select)
        {
            BeginSelect(p, mods);
        }
        else if (ToolInfo.CreatesMarkup(tool))
        {
            _mode = Mode.Drawing;
            _inkPoints.Clear();
            _inkPoints.Add(p);
            UpdateDrawingPreview(p, mods);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PointerMove(PagePoint p, PointerModifiers mods)
    {
        if (_mode == Mode.Idle) return;
        _last = p;
        switch (_mode)
        {
            case Mode.Drawing:
                if (_tool == ToolKind.Pen && DraftGeometry.Distance(_inkPoints[^1], p) >= _pixel * 1.5) _inkPoints.Add(p);
                UpdateDrawingPreview(p, mods);
                break;
            case Mode.Moving:
                var (dx, dy) = (p.X - _start.X, p.Y - _start.Y);
                Previews = _moving.Select(m => DraftGeometry.Translate(m.Draft!, dx, dy)).ToList();
                break;
            case Mode.Resizing:
                var target = mods.HasFlag(PointerModifiers.Shift) && _handle is HandleKind.LineStart or HandleKind.LineEnd
                    ? DraftGeometry.SnapAngle(OtherEnd(), p) : p;
                Previews = [DraftGeometry.DragHandle(_resizing!.Draft!, _handle, target, _pixel * 4)];
                break;
            case Mode.RubberBand:
                RubberBand = PageRect.FromPoints([_start, p]);
                break;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task PointerUpAsync(PagePoint p, PointerModifiers mods)
    {
        if (_mode == Mode.Idle) return;
        PointerMove(p, mods);
        var mode = _mode;
        var previews = Previews;
        var dragged = DraftGeometry.Distance(_start, p) > _pixel * ClickPixels;
        _mode = Mode.Idle;
        try
        {
            switch (mode)
            {
                case Mode.Drawing:
                    await FinishDrawingAsync(p, mods, dragged);
                    break;
                case Mode.Placing when _placement is { } template:
                    _placement = null;
                    var placed = await _doc.AddMarkupAsync(Page, DraftGeometry.Translate(template, _start.X, _start.Y));
                    _doc.Select(placed);
                    MarkupCreated?.Invoke(this, placed);
                    break;
                case Mode.Moving when dragged && _moving.Count > 0:
                    await _doc.ReplaceMarkupsAsync(_moving.Zip(previews, (m, d) => (m, d)).ToList(),
                        _moving.Count == 1 ? "Move markup" : $"Move {_moving.Count} markups");
                    break;
                case Mode.Resizing when dragged && _resizing != null:
                    await _doc.ReplaceMarkupAsync(_resizing, previews[0], "Resize markup");
                    break;
                case Mode.RubberBand when RubberBand is { } band:
                    var hits = _doc.MarkupsOnPage(Page).Where(m => DraftGeometry.Intersects(band, m.Bounds)).ToList();
                    _doc.SelectMany(hits, additive: mods.HasFlag(PointerModifiers.Control));
                    break;
            }
        }
        finally
        {
            Previews = [];
            RubberBand = null;
            _moving = [];
            _resizing = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Esc: 그리거나 옮기던 것을 취소한다.</summary>
    public void Cancel()
    {
        if (_placement != null) { _placement = null; Changed?.Invoke(this, EventArgs.Empty); }
        if (_mode == Mode.Idle) return;
        _mode = Mode.Idle;
        Previews = [];
        RubberBand = null;
        _moving = [];
        _resizing = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>선택 도구로 더블클릭: 글상자·Callout이면 글자 고치기.</summary>
    public void DoubleClick(PagePoint p, double pixelSize)
    {
        _pixel = pixelSize;
        var hit = HitTest(p);
        if (hit is { IsEditable: true, IsLocked: false, Draft: TextBoxDraft or CalloutDraft } item)
        {
            var (rect, text) = item.Draft switch
            {
                TextBoxDraft t => (t.Rect, t.Text),
                CalloutDraft c => (c.TextRect, c.Text),
                _ => throw new InvalidOperationException(),
            };
            TextEditRequested?.Invoke(this, new TextEditRequest(item.PageIndex, rect, text, item.Draft, item));
        }
    }

    /// <summary>글자 입력을 마쳤을 때. 비어 있으면 새 글상자는 만들지 않는다.</summary>
    public async Task CommitTextAsync(TextEditRequest request, string text)
    {
        if (request.Existing is { } existing)
        {
            if (text == request.InitialText) return;
            var updated = request.Draft switch
            {
                TextBoxDraft t => (MarkupDraft)(t with { Text = text }),
                CalloutDraft c => c with { Text = text },
                _ => request.Draft,
            };
            await _doc.ReplaceMarkupAsync(existing, updated, "Edit text");
            return;
        }
        if (string.IsNullOrWhiteSpace(text)) return;
        var draft = request.Draft switch
        {
            TextBoxDraft t => (MarkupDraft)(t with { Text = text }),
            CalloutDraft c => c with { Text = text },
            _ => request.Draft,
        };
        var item = await _doc.AddMarkupAsync(request.PageIndex, draft);
        _doc.Select(item);
        MarkupCreated?.Invoke(this, item);
    }

    /// <summary>현재 페이지에서 p에 있는 가장 위의 마크업.</summary>
    public MarkupItemViewModel? HitTest(PagePoint p)
    {
        var tol = _pixel * HitPixels;
        return _doc.MarkupsOnPage(Page).Reverse().FirstOrDefault(m =>
            m.Draft is { } d ? DraftGeometry.HitTest(d, p, tol) : DraftGeometry.Contains(m.Bounds.Inflate(tol), p));
    }

    /// <summary>p에 선택된 마크업의 손잡이가 있으면 그 종류 (마우스 모양을 바꾸는 데 쓴다).</summary>
    public HandleKind HandleUnder(PagePoint p, double pixelSize)
    {
        if (SingleResizable() is not { } item) return HandleKind.None;
        return DraftGeometry.HandleAt(item.Draft!, p, pixelSize * HitPixels);
    }

    // ---- 선택 도구 ----

    private void BeginSelect(PagePoint p, PointerModifiers mods)
    {
        var additive = mods.HasFlag(PointerModifiers.Control);
        if (!additive && SingleResizable() is { } sel)
        {
            var handle = DraftGeometry.HandleAt(sel.Draft!, p, _pixel * HitPixels);
            if (handle != HandleKind.None)
            {
                _mode = Mode.Resizing;
                _resizing = sel;
                _moving = [sel];
                _handle = handle;
                return;
            }
        }

        var hit = HitTest(p);
        if (hit == null)
        {
            if (!additive) _doc.ClearSelection();
            _mode = Mode.RubberBand;
            return;
        }
        if (additive)
        {
            _doc.Select(hit, additive: true);
            _mode = Mode.Idle;
            return;
        }
        if (!_doc.SelectedMarkups.Contains(hit)) _doc.Select(hit);
        _moving = _doc.SelectedMarkups.Where(m => m.PageIndex == Page && m.IsEditable && !m.IsLocked).ToList();
        _mode = _moving.Count > 0 ? Mode.Moving : Mode.Idle;
    }

    private MarkupItemViewModel? SingleResizable() =>
        _doc.SelectedMarkups.Count == 1 && _doc.SelectedMarkups[0] is { IsEditable: true, IsLocked: false } m && m.PageIndex == Page ? m : null;

    private PagePoint OtherEnd() => _resizing?.Draft is LineDraft l ? (_handle == HandleKind.LineStart ? l.End : l.Start) : _start;

    // ---- 그리기 도구 ----

    private void UpdateDrawingPreview(PagePoint p, PointerModifiers mods)
    {
        var draft = BuildDraft(p, mods, dragged: true);
        Previews = draft == null ? [] : [_defaults.Decorate(_tool, draft)];
    }

    private MarkupDraft? BuildDraft(PagePoint p, PointerModifiers mods, bool dragged)
    {
        var shift = mods.HasFlag(PointerModifiers.Shift);
        PageRect DragRect() => PageRect.FromPoints([_start, shift ? DraftGeometry.SnapSquare(_start, p) : p]);
        var text = _defaults.Get(_tool).DefaultText ?? "";
        switch (_tool)
        {
            case ToolKind.Rectangle: return new RectangleDraft(DragRect());
            case ToolKind.Ellipse: return new EllipseDraft(DragRect());
            case ToolKind.Highlight: return new HighlightDraft(DragRect());
            case ToolKind.Cloud: return CloudDraft.FromRect(DragRect());
            case ToolKind.Line:
            case ToolKind.Arrow:
                return new LineDraft(_start, shift ? DraftGeometry.SnapAngle(_start, p) : p, _tool == ToolKind.Arrow);
            case ToolKind.Pen:
                var pts = _inkPoints.ToList();
                if (DraftGeometry.Distance(pts[^1], p) > 1e-9) pts.Add(p);
                return new InkDraft([pts]);
            case ToolKind.TextBox:
                return new TextBoxDraft(dragged ? DragRect() : DefaultTextRect(_start), text);
            case ToolKind.Callout:
                // 누른 점 = 화살촉, 놓은 점 = 글상자 위치. 클릭만 하면 오른쪽 위에 상자를 둔다.
                var boxAt = dragged ? p : new PagePoint(_start.X + 60 * _pixel, _start.Y - 60 * _pixel);
                double w = DefaultTextWidthPixels * _pixel, h = DefaultTextHeightPixels * _pixel;
                var box = boxAt.X >= _start.X
                    ? new PageRect(boxAt.X, boxAt.Y - h / 2, boxAt.X + w, boxAt.Y + h / 2)
                    : new PageRect(boxAt.X - w, boxAt.Y - h / 2, boxAt.X, boxAt.Y + h / 2);
                return new CalloutDraft(box, text, _start);
            default:
                return null;
        }
    }

    private PageRect DefaultTextRect(PagePoint at) =>
        new(at.X, at.Y, at.X + DefaultTextWidthPixels * _pixel, at.Y + DefaultTextHeightPixels * _pixel);

    private async Task FinishDrawingAsync(PagePoint p, PointerModifiers mods, bool dragged)
    {
        var isText = _tool is ToolKind.TextBox or ToolKind.Callout;
        if (!dragged && !isText) return; // 도형은 클릭만으로 만들지 않는다 (실수 방지)
        if (_tool == ToolKind.Pen && _inkPoints.Count < 2) return;

        var draft = BuildDraft(p, mods, dragged && !(isText && _tool == ToolKind.TextBox && IsTiny(p)));
        if (draft == null) return;
        draft = _defaults.Decorate(_tool, draft);

        if (isText)
        {
            var rect = draft is CalloutDraft c ? c.TextRect : ((TextBoxDraft)draft).Rect;
            var text = draft is CalloutDraft c2 ? c2.Text : ((TextBoxDraft)draft).Text;
            TextEditRequested?.Invoke(this, new TextEditRequest(Page, rect, text, draft, null));
            return;
        }
        var item = await _doc.AddMarkupAsync(Page, draft);
        MarkupCreated?.Invoke(this, item);
    }

    private bool IsTiny(PagePoint p) =>
        Math.Abs(p.X - _start.X) < _pixel * 12 || Math.Abs(p.Y - _start.Y) < _pixel * 12;
}
