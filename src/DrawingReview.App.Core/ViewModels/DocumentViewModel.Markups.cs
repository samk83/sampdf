using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using DrawingReview.App.Core.Editing;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>
/// 문서의 마크업 편집: 추가·삭제·변경·상태·잠금, 되돌리기, 선택.
/// 모든 변경은 엔진(EngineGate) → 목록 갱신 → 화면 다시 그리기 → 되돌리기 기록 순서로 처리한다.
/// </summary>
public sealed partial class DocumentViewModel
{
    private UndoStack? _undo;

    /// <summary>문서 전체의 마크업 (모든 페이지).</summary>
    public ObservableCollection<MarkupItemViewModel> Markups { get; } = [];

    /// <summary>선택된 마크업 (캔버스·Review List·속성 패널이 공유).</summary>
    public ObservableCollection<MarkupItemViewModel> SelectedMarkups { get; } = [];

    public MarkupItemViewModel? SelectedMarkup => SelectedMarkups.Count > 0 ? SelectedMarkups[0] : null;

    public UndoStack Undo => _undo ??= CreateUndo();

    /// <summary>마크업 목록이 바뀌었을 때 (추가·삭제·변경). Review List가 다시 거른다.</summary>
    public event EventHandler? MarkupsChanged;

    public event EventHandler? SelectionChanged;

    private UndoStack CreateUndo()
    {
        var stack = new UndoStack();
        stack.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
        return stack;
    }

    /// <summary>문서를 연 뒤 모든 페이지의 마크업을 읽는다.</summary>
    public async Task LoadMarkupsAsync()
    {
        var all = await Gate.RunAsync(() => Document.GetAllMarkups()).ConfigureAwait(true);
        Markups.Clear();
        foreach (var info in all) Markups.Add(new MarkupItemViewModel(info, Pages[info.PageIndex].Info.Label));
        UpdateMarkupCounts();
        MarkupsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<MarkupItemViewModel> MarkupsOnPage(int pageIndex) => Markups.Where(m => m.PageIndex == pageIndex);

    /// <summary>
    /// Review List에서 고른 마크업을 보여준다: 해당 페이지로 이동하고, 너무 작게 보이면 확대, 아니면 가운데로.
    /// </summary>
    public void RevealMarkup(MarkupItemViewModel item)
    {
        GoToPage(item.PageIndex);
        var b = item.Bounds;
        var screenSize = Math.Max(b.Width, b.Height) * Viewport.Zoom;
        if (screenSize < 24 || Viewport.VisiblePageRegion() is null)
            Viewport.ZoomToRegion(b.Inflate(Math.Max(b.Width, b.Height)), maxZoom: Math.Max(Viewport.Zoom, Viewing.Viewport.ActualSizeZoom * 2));
        else
            Viewport.CenterOn(b.Center);
    }

    // ---- 선택 ----

    public void Select(MarkupItemViewModel? item, bool additive = false)
    {
        if (!additive) SelectedMarkups.Clear();
        if (item != null)
        {
            if (additive && SelectedMarkups.Contains(item)) SelectedMarkups.Remove(item);
            else if (!SelectedMarkups.Contains(item)) SelectedMarkups.Add(item);
        }
        RaiseSelectionChanged();
    }

    public void SelectMany(IEnumerable<MarkupItemViewModel> items, bool additive = false)
    {
        if (!additive) SelectedMarkups.Clear();
        foreach (var i in items)
            if (!SelectedMarkups.Contains(i)) SelectedMarkups.Add(i);
        RaiseSelectionChanged();
    }

    public void ClearSelection()
    {
        if (SelectedMarkups.Count == 0) return;
        SelectedMarkups.Clear();
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedMarkup));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- 편집 작업 ----

    /// <summary>새 마크업. 기본 상태는 Open. 되돌리기를 기록한다.</summary>
    public async Task<MarkupItemViewModel> AddMarkupAsync(int pageIndex, MarkupDraft draft, string status = ReviewStatus.Open)
    {
        var id = await Gate.RunAsync(() =>
        {
            var newId = Document.AddMarkup(pageIndex, draft);
            Document.SetMarkupStatus(pageIndex, newId, status);
            return newId;
        }).ConfigureAwait(true);
        var item = await InsertItemAsync(pageIndex, id);
        Changed(pageIndex);
        Undo.Push(new DelegateAction($"Add {MarkupItemViewModel.KindDisplayName(item.Kind)}",
            undo: async () => { await RemoveAsync(pageIndex, id); Changed(pageIndex); },
            redo: async () =>
            {
                await Gate.RunAsync(() => { Document.AddMarkup(pageIndex, draft, id); Document.SetMarkupStatus(pageIndex, id, status); });
                await InsertItemAsync(pageIndex, id);
                Changed(pageIndex);
            }));
        return item;
    }

    /// <summary>여러 마크업을 한 번에 추가 (붙여넣기). 하나의 되돌리기 단위. 추가된 항목을 돌려준다.</summary>
    public async Task<IReadOnlyList<MarkupItemViewModel>> AddMarkupsAsync(int pageIndex, IReadOnlyList<MarkupDraft> drafts, string name)
    {
        var created = new List<(string Id, MarkupDraft Draft)>();
        foreach (var d in drafts)
        {
            var id = await Gate.RunAsync(() =>
            {
                var newId = Document.AddMarkup(pageIndex, d);
                Document.SetMarkupStatus(pageIndex, newId, ReviewStatus.Open);
                return newId;
            });
            created.Add((id, d));
        }
        var items = new List<MarkupItemViewModel>();
        foreach (var (id, _) in created) items.Add(await InsertItemAsync(pageIndex, id));
        if (created.Count == 0) return items;
        Changed(pageIndex);
        Undo.Push(new DelegateAction(name,
            undo: async () =>
            {
                foreach (var (id, _) in created) await RemoveAsync(pageIndex, id);
                Changed(pageIndex);
            },
            redo: async () =>
            {
                foreach (var (id, d) in created)
                {
                    await Gate.RunAsync(() => { Document.AddMarkup(pageIndex, d, id); Document.SetMarkupStatus(pageIndex, id, ReviewStatus.Open); });
                    await InsertItemAsync(pageIndex, id);
                }
                Changed(pageIndex);
            }));
        return items;
    }

    /// <summary>선택한(또는 주어진) 마크업 삭제. 잠긴 것과 다른 프로그램의 주석은 건너뛴다. 삭제한 개수를 돌려준다.</summary>
    public async Task<int> DeleteMarkupsAsync(IEnumerable<MarkupItemViewModel> items)
    {
        var targets = items.Where(i => i.IsEditable && !i.IsLocked).Select(i => i.Info).ToList();
        if (targets.Count == 0) return 0;
        foreach (var t in targets) await RemoveAsync(t.PageIndex, t.Id);
        Changed(targets.Select(t => t.PageIndex));
        Undo.Push(new DelegateAction(targets.Count == 1 ? "Delete markup" : $"Delete {targets.Count} markups",
            undo: async () =>
            {
                foreach (var t in targets) await RestoreAsync(t);
                Changed(targets.Select(t => t.PageIndex));
            },
            redo: async () =>
            {
                foreach (var t in targets) await RemoveAsync(t.PageIndex, t.Id);
                Changed(targets.Select(t => t.PageIndex));
            }));
        return targets.Count;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var items = SelectedMarkups.ToList();
        ClearSelection();
        await DeleteMarkupsAsync(items);
    }

    private bool CanDeleteSelected() => SelectedMarkups.Any(m => m.IsEditable && !m.IsLocked);

    /// <summary>여러 마크업을 새 모양으로 바꾼다 (이동·크기·속성). 하나의 되돌리기 단위.</summary>
    public async Task ReplaceMarkupsAsync(IReadOnlyList<(MarkupItemViewModel Item, MarkupDraft Draft)> changes, string name)
    {
        var list = changes.Where(c => c.Item.IsEditable).Select(c => (Info: c.Item.Info, New: c.Draft)).ToList();
        if (list.Count == 0) return;
        foreach (var (info, draft) in list) await ReplaceAsync(info.PageIndex, info.Id, draft);
        Changed(list.Select(c => c.Info.PageIndex));
        Undo.Push(new DelegateAction(name,
            undo: async () =>
            {
                foreach (var (info, _) in list) await ReplaceAsync(info.PageIndex, info.Id, info.Draft!);
                Changed(list.Select(c => c.Info.PageIndex));
            },
            redo: async () =>
            {
                foreach (var (info, draft) in list) await ReplaceAsync(info.PageIndex, info.Id, draft);
                Changed(list.Select(c => c.Info.PageIndex));
            }));
    }

    public Task ReplaceMarkupAsync(MarkupItemViewModel item, MarkupDraft draft, string name) =>
        ReplaceMarkupsAsync([(item, draft)], name);

    /// <summary>검토 상태 일괄 변경 (기획서 8장: 일괄 변경에도 되돌리기 제공).</summary>
    public async Task SetStatusAsync(IEnumerable<MarkupItemViewModel> items, string status)
    {
        var list = items.Select(i => (Item: i, Old: i.Info.Status)).Where(x => x.Item.Status != status).ToList();
        if (list.Count == 0) return;
        async Task Apply(Func<(MarkupItemViewModel Item, string? Old), string?> pick)
        {
            foreach (var x in list)
            {
                var value = pick(x);
                await Gate.RunAsync(() => Document.SetMarkupStatus(x.Item.PageIndex, x.Item.Id, value));
                await RefreshItemAsync(x.Item.PageIndex, x.Item.Id);
            }
            Changed(list.Select(x => x.Item.PageIndex), rerender: false);
        }
        await Apply(_ => status);
        Undo.Push(new DelegateAction(list.Count == 1 ? $"Set status {status}" : $"Set status {status} ({list.Count})",
            undo: () => Apply(x => x.Old), redo: () => Apply(_ => status)));
    }

    public async Task SetLockedAsync(IEnumerable<MarkupItemViewModel> items, bool locked)
    {
        var list = items.Where(i => i.IsLocked != locked).ToList();
        if (list.Count == 0) return;
        async Task Apply(bool value)
        {
            foreach (var i in list)
            {
                await Gate.RunAsync(() => Document.SetMarkupLocked(i.PageIndex, i.Id, value));
                await RefreshItemAsync(i.PageIndex, i.Id);
            }
            Changed(list.Select(i => i.PageIndex), rerender: false);
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
        await Apply(locked);
        Undo.Push(new DelegateAction(locked ? "Lock markup" : "Unlock markup", () => Apply(!locked), () => Apply(locked)));
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync() => await Undo.UndoAsync();

    private bool CanUndo() => Undo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private async Task RedoAsync() => await Undo.RedoAsync();

    private bool CanRedo() => Undo.CanRedo;

    // ---- 내부 도우미 ----

    private async Task ReplaceAsync(int page, string id, MarkupDraft draft)
    {
        await Gate.RunAsync(() => Document.ReplaceMarkup(page, id, draft));
        await RefreshItemAsync(page, id);
    }

    private async Task RemoveAsync(int page, string id)
    {
        await Gate.RunAsync(() => Document.DeleteMarkup(page, id));
        var item = Markups.FirstOrDefault(m => m.Id == id && m.PageIndex == page);
        if (item != null)
        {
            Markups.Remove(item);
            if (SelectedMarkups.Remove(item)) RaiseSelectionChanged();
        }
    }

    private async Task RestoreAsync(MarkupInfo info)
    {
        await Gate.RunAsync(() =>
        {
            Document.AddMarkup(info.PageIndex, info.Draft!, info.Id);
            Document.SetMarkupStatus(info.PageIndex, info.Id, info.Status);
            if (info.IsLocked) Document.SetMarkupLocked(info.PageIndex, info.Id, true);
        });
        await InsertItemAsync(info.PageIndex, info.Id);
    }

    private async Task<MarkupItemViewModel> InsertItemAsync(int page, string id)
    {
        var info = await ReadAsync(page, id) ?? throw new InvalidOperationException($"Markup {id} was not found after saving it.");
        var item = new MarkupItemViewModel(info, Pages[page].Info.Label);
        // 페이지 순서를 유지해 Review List 기본 정렬과 맞춘다.
        var index = 0;
        while (index < Markups.Count && Markups[index].PageIndex <= page) index++;
        Markups.Insert(index, item);
        return item;
    }

    private async Task RefreshItemAsync(int page, string id)
    {
        var info = await ReadAsync(page, id);
        var item = Markups.FirstOrDefault(m => m.Id == id && m.PageIndex == page);
        if (info != null && item != null) item.Info = info;
    }

    private Task<MarkupInfo?> ReadAsync(int page, string id) =>
        Gate.RunAsync(() => Document.GetMarkups(page).FirstOrDefault(m => m.Id == id));

    private void Changed(int page, bool rerender = true) => Changed([page], rerender);

    private void Changed(IEnumerable<int> pages, bool rerender = true)
    {
        IsModified = true;
        if (rerender)
        {
            foreach (var p in pages.Distinct())
            {
                InvalidateRenderCache(p);
                _ = RefreshThumbnailAsync(p);
            }
        }
        UpdateMarkupCounts();
        MarkupsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateMarkupCounts()
    {
        var counts = Markups.GroupBy(m => m.PageIndex).ToDictionary(g => g.Key, g => g.Count());
        foreach (var p in Pages) p.MarkupCount = counts.GetValueOrDefault(p.Index);
    }

    private async Task RefreshThumbnailAsync(int pageIndex)
    {
        var page = Pages[pageIndex];
        var zoom = ThumbnailLongEdge / Math.Max(page.Info.Width, page.Info.Height);
        try { page.Image = await Gate.RunAsync(() => Document.RenderPage(pageIndex, zoom)).ConfigureAwait(true); }
        catch (ObjectDisposedException) { /* 탭을 닫는 중 */ }
    }
}
