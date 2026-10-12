using CommunityToolkit.Mvvm.ComponentModel;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.ViewModels;

/// <summary>왼쪽 Thumbnails 패널의 페이지 한 장.</summary>
public sealed partial class PageThumbnailViewModel(PageInfo info) : ObservableObject
{
    public PageInfo Info { get; private set; } = info;

    public int Index => Info.Index;

    /// <summary>"1  A-101"처럼 번호와 페이지 라벨.</summary>
    public string Caption => Info.Label is { } label && label != (Index + 1).ToString()
        ? $"{Index + 1}  {label}"
        : $"{Index + 1}";

    [ObservableProperty]
    private RenderedImage? _image;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private int _markupCount;
}
