namespace DrawingReview.App.Core.Editing;

/// <summary>되돌릴 수 있는 작업 하나.</summary>
public interface IUndoableAction
{
    /// <summary>메뉴·상태 표시줄에 보일 이름 ("Move markup" 등).</summary>
    string Name { get; }

    Task UndoAsync();

    Task RedoAsync();
}

/// <summary>람다 두 개로 만드는 간단한 작업.</summary>
public sealed class DelegateAction(string name, Func<Task> undo, Func<Task> redo) : IUndoableAction
{
    public string Name { get; } = name;
    public Task UndoAsync() => undo();
    public Task RedoAsync() => redo();
}

/// <summary>
/// Ctrl+Z / Ctrl+Y. 문서(탭)마다 하나씩 갖는다.
/// 새 작업을 기록하면 다시 실행 목록은 지워진다.
/// </summary>
public sealed class UndoStack(int limit = 200)
{
    private readonly LinkedList<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();
    private bool _busy;

    public bool CanUndo => _undo.Count > 0 && !_busy;
    public bool CanRedo => _redo.Count > 0 && !_busy;
    public string? UndoName => _undo.Last?.Value.Name;
    public string? RedoName => _redo.Count > 0 ? _redo.Peek().Name : null;

    public event EventHandler? Changed;

    /// <summary>이미 실행한 작업을 기록한다.</summary>
    public void Push(IUndoableAction action)
    {
        _undo.AddLast(action);
        while (_undo.Count > limit) _undo.RemoveFirst();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> UndoAsync()
    {
        if (!CanUndo) return false;
        var action = _undo.Last!.Value;
        _undo.RemoveLast();
        _busy = true;
        try { await action.UndoAsync(); }
        finally { _busy = false; }
        _redo.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> RedoAsync()
    {
        if (!CanRedo) return false;
        var action = _redo.Pop();
        _busy = true;
        try { await action.RedoAsync(); }
        finally { _busy = false; }
        _undo.AddLast(action);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
