using UnityEngine;

[ExecuteAlways]
public class ComicPreviewFrameProxy : MonoBehaviour
{
    [SerializeField] private ComicSequence previewSequence;
    [SerializeField] private int previewPageIndex = -1;
    [SerializeField] private int previewFrameIndex = -1;
    [SerializeField] private bool previewEditable;
    [SerializeField] private Vector2 baseCoverSize = Vector2.one;

    public ComicSequence PreviewSequence => previewSequence;
    public int PreviewPageIndex => previewPageIndex;
    public int PreviewFrameIndex => previewFrameIndex;
    public bool PreviewEditable => previewEditable;
    public Vector2 BaseCoverSize => baseCoverSize;
    public RectTransform RectTransform => transform as RectTransform;

    public void Configure(ComicSequence sequence, int pageIndex, int frameIndex, bool editable, Vector2 coverSize)
    {
        previewSequence = sequence;
        previewPageIndex = pageIndex;
        previewFrameIndex = frameIndex;
        previewEditable = editable;
        baseCoverSize = new Vector2(
            Mathf.Max(1f, coverSize.x),
            Mathf.Max(1f, coverSize.y));
    }

    public void ClearBinding()
    {
        previewSequence = null;
        previewPageIndex = -1;
        previewFrameIndex = -1;
        previewEditable = false;
        baseCoverSize = Vector2.one;
    }

    public bool TryGetFrame(out ComicFrame frame)
    {
        frame = default;
        if (!HasValidBinding())
            return false;

        var pages = previewSequence.pages;
        var page = pages[previewPageIndex];
        if (previewFrameIndex == 0) frame = page.frame0;
        else if (previewFrameIndex == 1) frame = page.frame1;
        else frame = page.frame2;
        return true;
    }

    public bool SaveTransform(Vector2 imageOffset, Vector2 imageScale)
    {
        if (!HasValidBinding())
            return false;

        var pages = previewSequence.pages;
        var page = pages[previewPageIndex];
        var frame = previewFrameIndex == 0 ? page.frame0 : previewFrameIndex == 1 ? page.frame1 : page.frame2;

        frame.imageOffset = imageOffset;
        frame.imageScale = imageScale;

        if (previewFrameIndex == 0) page.frame0 = frame;
        else if (previewFrameIndex == 1) page.frame1 = frame;
        else page.frame2 = frame;

        pages[previewPageIndex] = page;
        return true;
    }

    private bool HasValidBinding()
    {
        return previewEditable &&
               previewSequence != null &&
               previewSequence.pages != null &&
               previewPageIndex >= 0 &&
               previewPageIndex < previewSequence.pages.Count &&
               previewFrameIndex >= 0 &&
               previewFrameIndex <= 2;
    }
}
