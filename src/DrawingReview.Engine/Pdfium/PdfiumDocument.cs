using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdf;
using DrawingReview.Engine.Persistence;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdfium;

/// <summary>
/// 열린 PDF 하나. 구조는 세 겹이다:
/// - 현재 파일 바이트(_bytes): 저장하면 그대로 파일이 되는 내용.
/// - PDFsharp 모델(_model): 주석 추가·수정·삭제의 원본. 바뀐 객체는 ChangeTracker가 기억하고,
///   그릴 때나 저장할 때 IncrementalWriter가 _bytes 뒤에 덧붙인다 (PDFsharp Save는 한 번만 가능해서 전체 저장은 쓰지 않는다).
/// - PDFium 보기(_view): 화면 그리기·글자·검색·책갈피. _bytes가 바뀌면 새로 연다.
/// 암호가 필요한 문서는 Authenticate 전까지 보기도 모델도 없다. 암호 문서는 모델을 읽은 뒤 암호를 푼 바이트로 바꿔 두고
/// (증분 저장은 암호화된 파일에 덧붙일 수 없다), 저장할 때 입력한 암호로 다시 잠근다.
/// PDFsharp가 읽지 못하는 파일은 보기만 가능(편집 불가)하다.
/// </summary>
internal sealed class PdfiumDocument : IPdfDocument
{
    private byte[] _bytes;
    private PdfiumView? _view;
    private PdfDocument? _model;
    private string? _password;
    private bool _dirty;
    private string? _editProblem;
    private readonly bool _isEncrypted;
    private bool _needsPassword;
    private readonly Dictionary<int, PageGeometry> _geometry = new();

    public PdfiumDocument(byte[] pdfBytes, string? sourcePath)
    {
        _bytes = pdfBytes;
        SourcePath = sourcePath;
        _view = PdfiumView.Open(pdfBytes, null, out _needsPassword);
        _isEncrypted = _needsPassword || _view!.SecurityRevision >= 0;
        if (_view != null) LoadModel();
    }

    public string? SourcePath { get; }

    public int PageCount => _view?.PageCount ?? 0;

    public DocumentSecurityInfo Security => new(_isEncrypted, _needsPassword, HasSignatures(), _editProblem);

    public bool Authenticate(string password)
    {
        if (!_needsPassword) return true;
        var view = PdfiumView.Open(_bytes, password, out var stillNeeds);
        if (view == null || stillNeeds) return false;
        _view = view;
        _password = password;
        _needsPassword = false;
        LoadModel();
        return true;
    }

    private void LoadModel()
    {
        try
        {
            if (_isEncrypted)
            {
                // 암호를 푼 바이트로 바꿔 둔다 (PDFium 사본): 이후 편집은 이 바이트에 증분으로 덧붙이고, 저장할 때 다시 암호를 건다.
                // PDFsharp는 사용자 암호만으로는 편집을 거부하므로(소유자 암호 요구) 평문으로 만든 뒤 연다.
                _bytes = ReadView.SaveDecryptedCopy();
                ReopenView();
            }
            _model = OpenModel(_bytes);
            _editProblem = null;
        }
        catch (Exception ex)
        {
            // 보기는 되지만 편집은 못 하는 파일. 이유를 경고로 알린다.
            _model = null;
            _editProblem = ex.Message;
        }
    }

    private static PdfDocument OpenModel(byte[] bytes) => PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);

    /// <summary>_bytes로 보기를 다시 연다 (암호를 푼 바이트면 암호 없이 열린다).</summary>
    private void ReopenView()
    {
        var fresh = PdfiumView.Open(_bytes, null, out var needsPassword);
        if (needsPassword) fresh = PdfiumView.Open(_bytes, _password, out _);
        if (fresh == null) throw new InvalidDataException("The edited PDF could not be reopened.");
        _view?.Dispose();
        _view = fresh;
    }

    // ---- 보기 ----

    private PdfiumView ReadView => _view ?? throw new InvalidOperationException("This PDF is password protected. Call Authenticate first.");

    /// <summary>편집 결과가 반영된 보기. 모델이 바뀐 뒤면 바뀐 객체를 바이트에 덧붙이고 새로 연다.</summary>
    private PdfiumView CurrentView()
    {
        Sync();
        return ReadView;
    }

    /// <summary>모델의 바뀐 객체를 _bytes에 증분으로 덧붙이고 보기를 새로 연다.</summary>
    private void Sync()
    {
        if (!_dirty || _model == null) return;
        var changed = ChangeTracker.For(_model).Take();
        if (changed.Count > 0)
        {
            _bytes = IncrementalWriter.Append(_bytes, _model, changed);
            ReopenView();
        }
        _dirty = false;
    }

    private PageGeometry Geometry(int pageIndex)
    {
        if (_geometry.TryGetValue(pageIndex, out var g)) return g;
        return _geometry[pageIndex] = ReadView.Geometry(pageIndex);
    }

    public PageInfo GetPageInfo(int pageIndex)
    {
        var g = Geometry(pageIndex);
        return new PageInfo(pageIndex, g.Width, g.Height, g.Rotation, ReadView.PageLabel(pageIndex));
    }

    public byte[] RenderPagePng(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true) =>
        PngEncoder.Encode(RenderPage(pageIndex, zoom, clip, includeMarkups));

    public RenderedImage RenderPage(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true) =>
        CurrentView().Render(pageIndex, zoom, clip, includeMarkups);

    public IReadOnlyList<OutlineItem> GetOutline()
    {
        try { return ReadView.Outline(); }
        catch (Exception) { return []; } // 손상된 목차는 무시하고 문서는 그대로 보여준다
    }

    // 글자·검색은 페이지 내용만 보므로 주석 편집과 무관하다 → 다시 열지 않은 보기로 충분하다.
    public IReadOnlyList<TextWord> GetWords(int pageIndex) => ReadView.Words(pageIndex);

    public IReadOnlyList<PageRect> SearchText(int pageIndex, string needle) =>
        string.IsNullOrEmpty(needle) ? [] : ReadView.Search(pageIndex, needle, maxHits: 1000);

    // ---- 주석 ----

    private PdfDocument Model => _model ?? throw new InvalidOperationException(
        _needsPassword ? "This PDF is password protected. Call Authenticate first." : $"This PDF cannot be edited: {_editProblem}");

    private PdfPage ModelPage(int pageIndex)
    {
        var count = Model.PageCount;
        if (pageIndex < 0 || pageIndex >= count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"Page index must be 0..{count - 1}.");
        return Model.Pages[pageIndex];
    }

    private static PdfArray? Annots(PdfPage page) => Array(page.Elements.ContainsKey("/Annots") ? page.Elements["/Annots"] : null);

    /// <summary>
    /// 페이지의 주석 사전들. 말풍선(Popup), 링크(Link), 폼 필드(Widget)는 마크업 목록에 넣지 않는다 (보존만 한다).
    /// 배열 항목 자체도 함께 돌려줘 삭제에 쓴다.
    /// </summary>
    private static IEnumerable<(PdfDictionary Annot, PdfItem Item, string Id)> Annotations(PdfPage page, int pageIndex)
    {
        if (Annots(page) is not { } annots) yield break;
        var items = annots.Elements.Items;
        for (int k = 0; k < items.Length; k++)
        {
            if (Dict(items[k]) is not { } annot) continue;
            if (Name(annot, "/Subtype") is "/Popup" or "/Link" or "/Widget") continue;
            yield return (annot, items[k], IdOf(annot, pageIndex, k));
        }
    }

    private static string IdOf(PdfDictionary annot, int pageIndex, int index)
    {
        if (Text(annot, "/NM") is { } nm) return nm;
        var number = annot.Reference != null ? PdfInternals.GetObjectNumber(annot) : 0;
        return number > 0 ? $"obj:{number}" : $"page{pageIndex}:annot{index}";
    }

    private (PdfDictionary Annot, PdfItem Item)? Find(PdfPage page, int pageIndex, string markupId)
    {
        foreach (var (annot, item, id) in Annotations(page, pageIndex))
            if (id == markupId) return (annot, item);
        return null;
    }

    public string AddMarkup(int pageIndex, MarkupDraft draft, string? id = null)
    {
        var page = ModelPage(pageIndex);
        var result = AnnotationWriter.Add(Model, page, Geometry(pageIndex), draft, id);
        _dirty = true;
        return result;
    }

    public bool ReplaceMarkup(int pageIndex, string markupId, MarkupDraft draft)
    {
        var page = ModelPage(pageIndex);
        if (Find(page, pageIndex, markupId) is not { } old) return false;
        var status = AnnotationReader.ReadStatus(old.Annot);
        var locked = AnnotationReader.ReadLocked(old.Annot);
        var created = Text(old.Annot, "/CreationDate");
        Remove(page, old.Annot, old.Item);
        AnnotationWriter.Add(Model, page, Geometry(pageIndex), draft, markupId);
        if (Find(page, pageIndex, markupId) is { } fresh)
        {
            if (created != null) fresh.Annot.Elements["/CreationDate"] = String(created);
            if (status != null) fresh.Annot.Elements[AnnotationWriter.StatusKey] = String(status);
            if (locked) AnnotationWriter.SetLocked(Model, fresh.Annot, true);
            Touch(Model, fresh.Annot);
        }
        _dirty = true;
        return true;
    }

    public bool SetMarkupStatus(int pageIndex, string markupId, string? status)
    {
        if (Find(ModelPage(pageIndex), pageIndex, markupId) is not { } found) return false;
        AnnotationWriter.SetStatus(Model, found.Annot, status);
        _dirty = true;
        return true;
    }

    public bool SetMarkupLocked(int pageIndex, string markupId, bool locked)
    {
        if (Find(ModelPage(pageIndex), pageIndex, markupId) is not { } found) return false;
        AnnotationWriter.SetLocked(Model, found.Annot, locked);
        _dirty = true;
        return true;
    }

    public IReadOnlyList<MarkupInfo> GetMarkups(int pageIndex)
    {
        if (_model == null) return [];
        var page = ModelPage(pageIndex);
        var g = Geometry(pageIndex);
        return Annotations(page, pageIndex).Select(a => AnnotationReader.Read(a.Annot, pageIndex, g, a.Id)).ToList();
    }

    public IReadOnlyList<MarkupInfo> GetAllMarkups() =>
        Enumerable.Range(0, PageCount).SelectMany(GetMarkups).ToList();

    public bool DeleteMarkup(int pageIndex, string markupId)
    {
        var page = ModelPage(pageIndex);
        if (Find(page, pageIndex, markupId) is not { } found) return false;
        Remove(page, found.Annot, found.Item);
        _dirty = true;
        return true;
    }

    private void Remove(PdfPage page, PdfDictionary annot, PdfItem item)
    {
        if (Annots(page) is not { } annots) return;
        annots.Elements.Remove(item);
        // 딸린 말풍선(Popup)도 함께 지운다
        if (annot.Elements.ContainsKey("/Popup") && Dict(annot.Elements["/Popup"]) is { } popup)
        {
            foreach (var candidate in annots.Elements.Items)
                if (Dict(candidate) == popup) { annots.Elements.Remove(candidate); break; }
        }
        AnnotationWriter.TouchAnnotsOwner(Model, page);
    }

    private static int CountAnnotations(PdfDocument model)
    {
        var total = 0;
        for (int i = 0; i < model.PageCount; i++) total += Annots(model.Pages[i])?.Elements.Count ?? 0;
        return total;
    }

    // ---- 저장 ----

    public void SaveTo(string path)
    {
        if (_model == null)
        {
            // 편집할 수 없는 문서는 읽은 바이트를 그대로 복사한다
            var bytes = _bytes;
            SafeFileSaver.Save(path, temp => File.WriteAllBytes(temp, bytes), temp => Verify(temp, PageCount, null));
            return;
        }
        Sync();
        var expectedPages = Model.PageCount;
        var expectedAnnots = CountAnnotations(Model);
        SafeFileSaver.Save(path, temp => WriteBytes(temp, flatten: false), temp => Verify(temp, expectedPages, expectedAnnots));
    }

    public void ExportFlattened(string path)
    {
        var pages = PageCount;
        if (_model == null) throw new InvalidOperationException($"This PDF cannot be flattened: {_editProblem ?? "password required"}");
        Sync();
        SafeFileSaver.Save(path, temp => WriteBytes(temp, flatten: true), temp => Verify(temp, pages, null));
    }

    /// <summary>
    /// 현재 바이트(편집 반영됨)를 파일로 쓴다. 평탄화나 암호 복원이 필요하면 사본을 열어 거기서 처리한다
    /// (열린 모델은 그대로 둬서 이후 편집에 영향이 없게).
    /// </summary>
    private void WriteBytes(string temp, bool flatten)
    {
        var bytes = _bytes;
        var reencrypt = _isEncrypted && _password != null;
        if (!flatten && !reencrypt)
        {
            File.WriteAllBytes(temp, bytes);
            return;
        }
        using var copy = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        if (flatten) Flattener.Flatten(copy);
        if (reencrypt)
        {
            // 암호가 있던 문서는 입력한 암호로 다시 잠근다 (AES-256)
            copy.SecurityHandler.SetEncryptionToV5();
            copy.SecuritySettings.UserPassword = _password!;
            copy.SecuritySettings.OwnerPassword = _password!;
        }
        using var fs = File.Create(temp);
        copy.Save(fs, closeStream: false);
    }

    /// <summary>저장된 파일을 다시 열어 페이지 수(와 주석 수)가 그대로인지 확인한다.</summary>
    private void Verify(string temp, int expectedPages, int? expectedAnnots)
    {
        var bytes = File.ReadAllBytes(temp);
        using var check = PdfiumView.Open(bytes, _password, out var needsPassword)
            ?? throw new IOException("Save verification failed: the saved file could not be opened" + (needsPassword ? " (password)." : "."));
        if (check.PageCount != expectedPages)
            throw new IOException($"Save verification failed: expected {expectedPages} pages, found {check.PageCount}.");
        if (expectedAnnots is { } expected)
        {
            var annots = Enumerable.Range(0, check.PageCount).Sum(check.AnnotationCount);
            if (annots != expected)
                throw new IOException($"Save verification failed: expected {expected} annotations, found {annots}.");
        }
    }

    private bool HasSignatures()
    {
        try
        {
            if (_model == null) return false;
            var acro = Dict(_model.Internals.Catalog.Elements.ContainsKey("/AcroForm") ? _model.Internals.Catalog.Elements["/AcroForm"] : null);
            return acro != null && ((int)(Number(acro, "/SigFlags") ?? 0) & 1) != 0;
        }
        catch (Exception) { return false; }
    }

    public void Dispose()
    {
        _view?.Dispose();
        _view = null;
        _model?.Dispose();
        _model = null;
    }
}
