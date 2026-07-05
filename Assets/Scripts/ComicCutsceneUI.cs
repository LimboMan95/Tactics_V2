using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ComicCutsceneUI : MonoBehaviour, IPointerClickHandler
{
    [System.Serializable]
    public class FrameSlot
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image image;
        public RectTransform spriteRoot;
        public RectTransform textPlateRoot;
        public CanvasGroup textGroup;
        public Image textPlateBackground;
        public TMP_Text text;
    }

    [Header("Optional Auto-Build")]
    public bool buildIfMissingInPlayMode = true;

    [Header("Visual")]
    [Min(0)] public int frameBorderPixels = 4;
    [Range(0f, 1f)] public float textPlateAlpha = 0.85f;
    public TMP_FontAsset frameTextFontOverride;
    [Min(0f)] public float frameTextFontSizeOverride = 0f;

    [Header("Layout References")]
    public Canvas rootCanvas;
    public CanvasScaler canvasScaler;
    public GraphicRaycaster raycaster;
    public CanvasGroup screenGroup;
    public Image dimBackground;
    public RectTransform contentRoot;
    public FrameSlot frame0;
    public FrameSlot frame1;
    public FrameSlot frame2;
    public Button skipButton;
    public TMP_Text skipButtonText;

    private float _fadeDuration = 0.4f;
    private bool _isAnimating;
    private bool _isActive;
    private int _animationCount;
    private ComicSequence[] _sequenceQueue;
    private int _sequenceIndex;
    private int _pageIndex;
    private int _revealIndex;
    private int _nextSceneBuildIndex;
    private bool _loadSceneOnFinish;
    private bool _defaultsCaptured;
    private TMP_FontAsset _defaultFrameTextFont;
    private float _defaultFrameTextFontSize;
    private bool _isPreviewMode;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        ForceRuntimeReset();
    }

    public void Initialize(float fadeDuration)
    {
        _fadeDuration = Mathf.Max(0f, fadeDuration);

        if (Application.isPlaying && buildIfMissingInPlayMode)
        {
            BuildIfMissing();
        }
        CaptureDefaultsIfNeeded();
        RefreshBorders();

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
        }

        HideImmediate();
    }

    private void ForceRuntimeReset()
    {
        _isActive = false;
        _isAnimating = false;
        _animationCount = 0;
        _isPreviewMode = false;
        _sequenceQueue = null;
        _sequenceIndex = 0;
        _pageIndex = 0;
        _revealIndex = 0;

        ResetDisplayState(clearContent: true);
        SetUiVisible(false);
    }

    public void RefreshBorders()
    {
        CaptureDefaultsIfNeeded();
        ApplyBorder(frame0);
        ApplyBorder(frame1);
        ApplyBorder(frame2);
        ApplyTextPlateStyle(frame0);
        ApplyTextPlateStyle(frame1);
        ApplyTextPlateStyle(frame2);
        ApplyTextStyle(frame0);
        ApplyTextStyle(frame1);
        ApplyTextStyle(frame2);
    }

    public void EnsureBuiltForPreview()
    {
        BuildIfMissing();
        CaptureDefaultsIfNeeded();
        RefreshBorders();
    }

    public void ShowPreviewPage(ComicPage page, int visibleFrames)
    {
        EnsureBuiltForPreview();

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SetUiVisible(true);
        _isActive = false;
        _isPreviewMode = true;

        if (screenGroup != null)
        {
            screenGroup.alpha = 1f;
            screenGroup.blocksRaycasts = false;
            screenGroup.interactable = false;
        }

        if (dimBackground != null)
        {
            dimBackground.color = new Color(0f, 0f, 0f, 1f);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
        }

        ApplyFrameToSlot(frame0, page.frame0);
        ApplyFrameToSlot(frame1, page.frame1);
        ApplyFrameToSlot(frame2, page.frame2);

        SetSlotPreview(frame0, page.frame0, visibleFrames >= 1);
        SetSlotPreview(frame1, page.frame1, visibleFrames >= 2);
        SetSlotPreview(frame2, page.frame2, visibleFrames >= 3);
    }

    public void ClearPreview()
    {
        HidePageImmediate();
        ClearSlotContent(frame0);
        ClearSlotContent(frame1);
        ClearSlotContent(frame2);
        if (screenGroup != null)
        {
            screenGroup.alpha = 0f;
            screenGroup.blocksRaycasts = true;
            screenGroup.interactable = true;
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
        }

        _isPreviewMode = false;
        SetUiVisible(false);
    }

    public void Play(ComicSequence[] sequences, int nextSceneBuildIndex, bool loadSceneOnFinish)
    {
        if (sequences == null || sequences.Length == 0) return;
        if (_isActive) return;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        _isPreviewMode = false;
        _sequenceQueue = sequences;
        _sequenceIndex = 0;
        _pageIndex = 0;
        _revealIndex = 0;
        _nextSceneBuildIndex = nextSceneBuildIndex;
        _loadSceneOnFinish = loadSceneOnFinish;
        _animationCount = 0;
        _isAnimating = false;

        ResetDisplayState(clearContent: true);
        SetUiVisible(true);

        _isActive = true;

        StartCoroutine(PlayRoutine());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isActive) return;
        if (_isAnimating) return;
        StartCoroutine(AdvanceRoutine());
    }

    public void Skip()
    {
        if (!_isActive) return;
        if (_isAnimating) return;
        StartCoroutine(FinishRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        HidePageImmediate();

        PushAnimation();
        if (screenGroup != null) screenGroup.alpha = 0f;
        yield return FadeCanvasGroup(screenGroup, 1f, _fadeDuration);
        yield return LoadCurrentPage();
        PopAnimation();
    }

    private IEnumerator AdvanceRoutine()
    {
        if (_sequenceQueue == null || _sequenceQueue.Length == 0) yield break;

        var seq = _sequenceQueue[_sequenceIndex];
        if (seq == null || seq.pages == null || seq.pages.Count == 0)
        {
            yield return FinishRoutine();
            yield break;
        }

        var page = seq.pages[_pageIndex];

        if (_revealIndex < 3)
        {
            var frame = page.GetFrame(_revealIndex);
            FrameSlot slot = GetSlot(_revealIndex);
            yield return RevealSlot(slot, frame);
            _revealIndex++;
            yield break;
        }

        bool hasNextPage = _pageIndex + 1 < seq.pages.Count;
        if (hasNextPage)
        {
            yield return FadeOutPage();
            _pageIndex++;
            _revealIndex = 0;
            yield return LoadCurrentPage();
            yield break;
        }

        bool hasNextSequence = _sequenceIndex + 1 < _sequenceQueue.Length;
        if (hasNextSequence)
        {
            yield return FadeOutPage();
            _sequenceIndex++;
            _pageIndex = 0;
            _revealIndex = 0;
            yield return LoadCurrentPage();
            yield break;
        }

        yield return FinishRoutine();
    }

    private IEnumerator LoadCurrentPage()
    {
        var seq = _sequenceQueue[_sequenceIndex];
        if (seq == null || seq.pages == null || seq.pages.Count == 0)
        {
            yield return FinishRoutine();
            yield break;
        }

        var page = seq.pages[_pageIndex];
        ApplyFrameToSlot(frame0, page.frame0);
        ApplyFrameToSlot(frame1, page.frame1);
        ApplyFrameToSlot(frame2, page.frame2);

        SetSlotAlpha(frame0, 0f);
        SetSlotAlpha(frame1, 0f);
        SetSlotAlpha(frame2, 0f);

        _revealIndex = 0;
        yield return RevealSlot(frame0, page.frame0);
        _revealIndex = 1;
    }

    private IEnumerator RevealSlot(FrameSlot slot, ComicFrame frame)
    {
        if (slot == null) yield break;
        if (slot.root == null) yield break;
        if (slot.group == null) yield break;

        if (!slot.root.gameObject.activeSelf) slot.root.gameObject.SetActive(true);

        PushAnimation();

        if (frame.sprite != null && slot.image != null)
        {
            if (slot.image.sprite != frame.sprite) slot.image.sprite = frame.sprite;
            if (!slot.image.enabled) slot.image.enabled = true;
        }
        else if (slot.image != null)
        {
            slot.image.enabled = false;
        }

        if (slot.image != null)
        {
            var fitter = slot.image.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                if (frame.sprite != null)
                {
                    var rect = frame.sprite.rect;
                    fitter.aspectRatio = rect.height <= 0f ? 1f : rect.width / rect.height;
                }
                else
                {
                    fitter.aspectRatio = 1f;
                }
            }
        }

        bool wantsText = frame.showTextPlate && !string.IsNullOrWhiteSpace(frame.frameText);
        if (slot.textPlateRoot != null) slot.textPlateRoot.gameObject.SetActive(wantsText);
        if (slot.text != null) slot.text.text = wantsText ? frame.frameText : string.Empty;

        float targetAlpha = 1f;
        yield return FadeCanvasGroup(slot.group, targetAlpha, _fadeDuration);

        if (wantsText && slot.textGroup != null)
        {
            slot.textGroup.alpha = 0f;
            yield return FadeCanvasGroup(slot.textGroup, 1f, _fadeDuration);
        }

        PopAnimation();
    }

    private IEnumerator FadeOutPage()
    {
        PushAnimation();
        yield return FadeCanvasGroup(frame0.group, 0f, _fadeDuration);
        yield return FadeCanvasGroup(frame1.group, 0f, _fadeDuration);
        yield return FadeCanvasGroup(frame2.group, 0f, _fadeDuration);
        HidePageImmediate();
        PopAnimation();
    }

    private IEnumerator FinishRoutine()
    {
        PushAnimation();
        yield return FadeOutPage();
        if (_loadSceneOnFinish)
        {
            int target = _nextSceneBuildIndex;
            if (target < 0 || target >= SceneManager.sceneCountInBuildSettings)
            {
                target = 0;
            }

            if (screenGroup != null) screenGroup.alpha = 1f;
            yield return LoadSceneAsync(target);
            yield return null;
            yield return FadeCanvasGroup(screenGroup, 0f, _fadeDuration);
            HideImmediate();
            PopAnimation();
            yield break;
        }

        yield return FadeCanvasGroup(screenGroup, 0f, _fadeDuration);
        HideImmediate();
        PopAnimation();
    }

    private static IEnumerator LoadSceneAsync(int buildIndex)
    {
        var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        if (op == null) yield break;
        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }
    }

    private void HideImmediate()
    {
        _isActive = false;
        _sequenceQueue = null;
        _sequenceIndex = 0;
        _pageIndex = 0;
        _revealIndex = 0;
        _isPreviewMode = false;

        ResetDisplayState(clearContent: true);
        SetUiVisible(false);
    }

    private void HidePageImmediate()
    {
        SetSlotAlpha(frame0, 0f);
        SetSlotAlpha(frame1, 0f);
        SetSlotAlpha(frame2, 0f);
    }

    private static void SetSlotAlpha(FrameSlot slot, float alpha)
    {
        if (slot == null) return;
        if (slot.group != null) slot.group.alpha = alpha;
        if (slot.textGroup != null) slot.textGroup.alpha = 0f;
    }

    private void ResetDisplayState(bool clearContent)
    {
        HidePageImmediate();

        if (clearContent)
        {
            ClearSlotContent(frame0);
            ClearSlotContent(frame1);
            ClearSlotContent(frame2);
        }

        if (screenGroup != null)
        {
            screenGroup.alpha = 0f;
            screenGroup.blocksRaycasts = true;
            screenGroup.interactable = true;
        }

        if (dimBackground != null)
        {
            dimBackground.color = new Color(0f, 0f, 0f, 1f);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
        }
    }

    private void SetUiVisible(bool visible)
    {
        if (rootCanvas != null) rootCanvas.enabled = visible;
        if (raycaster != null) raycaster.enabled = visible;
    }

    private static void ClearSlotContent(FrameSlot slot)
    {
        if (slot == null) return;
        if (slot.group != null) slot.group.alpha = 0f;
        if (slot.image != null)
        {
            slot.image.enabled = false;
            slot.image.sprite = null;
        }

        if (slot.text != null)
        {
            slot.text.text = string.Empty;
        }

        if (slot.textPlateRoot != null)
        {
            slot.textPlateRoot.gameObject.SetActive(false);
        }

        if (slot.root != null)
        {
            slot.root.gameObject.SetActive(false);
        }
    }

    private static void SetSlotPreview(FrameSlot slot, ComicFrame frame, bool visible)
    {
        if (slot == null) return;
        if (slot.root != null) slot.root.gameObject.SetActive(visible);
        if (slot.group != null) slot.group.alpha = visible ? 1f : 0f;

        bool wantsText = visible && frame.showTextPlate && !string.IsNullOrWhiteSpace(frame.frameText);
        if (slot.textPlateRoot != null) slot.textPlateRoot.gameObject.SetActive(wantsText);
        if (slot.textGroup != null) slot.textGroup.alpha = wantsText ? 1f : 0f;
    }

    private FrameSlot GetSlot(int index)
    {
        if (index == 0) return frame0;
        if (index == 1) return frame1;
        return frame2;
    }

    private static void ApplyFrameToSlot(FrameSlot slot, ComicFrame frame)
    {
        if (slot == null) return;
        if (slot.root != null && !slot.root.gameObject.activeSelf) slot.root.gameObject.SetActive(true);

        ApplyBorder(slot);
        ApplyTextPlateStyle(slot);
        ApplyTextStyle(slot);

        if (slot.image != null)
        {
            slot.image.sprite = frame.sprite;
            slot.image.enabled = frame.sprite != null;
            slot.image.preserveAspect = false;
        }

        bool wantsText = frame.showTextPlate && !string.IsNullOrWhiteSpace(frame.frameText);
        if (slot.textPlateRoot != null) slot.textPlateRoot.gameObject.SetActive(wantsText);
        if (slot.text != null) slot.text.text = wantsText ? frame.frameText : string.Empty;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;

        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            group.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }
        group.alpha = target;
    }

    private void BuildIfMissing()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInChildren<Canvas>(true);
        if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 2000;

        if (canvasScaler == null) canvasScaler = rootCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        if (raycaster == null) raycaster = rootCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = rootCanvas.gameObject.AddComponent<GraphicRaycaster>();

        if (screenGroup == null) screenGroup = rootCanvas.GetComponent<CanvasGroup>();
        if (screenGroup == null) screenGroup = rootCanvas.gameObject.AddComponent<CanvasGroup>();
        screenGroup.alpha = 0f;
        screenGroup.interactable = true;
        screenGroup.blocksRaycasts = true;

        if (dimBackground == null)
        {
            var bgGo = new GameObject("DimBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(rootCanvas.transform, false);
            dimBackground = bgGo.GetComponent<Image>();
            dimBackground.color = new Color(0f, 0f, 0f, 1f);
            dimBackground.raycastTarget = true;
            var rt = (RectTransform)bgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            dimBackground.color = new Color(0f, 0f, 0f, 1f);
            dimBackground.raycastTarget = true;
        }

        if (contentRoot == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(rootCanvas.transform, false);
            contentRoot = contentGo.GetComponent<RectTransform>();
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;
        }

        BuildLayoutIfMissing();
        BuildSkipButtonIfMissing();
    }

    private void BuildLayoutIfMissing()
    {
        if (contentRoot == null) return;

        CleanupDuplicateGeneratedLayout();

        if (HasValidBuiltLayout())
        {
            return;
        }

        frame0 = new FrameSlot();
        frame1 = new FrameSlot();
        frame2 = new FrameSlot();

        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(contentRoot, false);
        var row = rowGo.GetComponent<HorizontalLayoutGroup>();
        row.childControlHeight = true;
        row.childControlWidth = true;
        row.childForceExpandHeight = true;
        row.childForceExpandWidth = true;
        row.spacing = 0f;
        row.padding = new RectOffset(0, 0, 0, 0);
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;

        var left = BuildFrameContainer("Frame0_BigLeft", rowGo.transform, out frame0);
        var leftLe = left.gameObject.AddComponent<LayoutElement>();
        leftLe.flexibleWidth = 1f;
        leftLe.minWidth = 0f;

        var rightColGo = new GameObject("RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rightColGo.transform.SetParent(rowGo.transform, false);
        var rightCol = rightColGo.GetComponent<VerticalLayoutGroup>();
        rightCol.childControlHeight = true;
        rightCol.childControlWidth = true;
        rightCol.childForceExpandHeight = true;
        rightCol.childForceExpandWidth = true;
        rightCol.spacing = 0f;
        rightCol.padding = new RectOffset(0, 0, 0, 0);
        var rightLe = rightColGo.gameObject.AddComponent<LayoutElement>();
        rightLe.flexibleWidth = 1f;
        rightLe.minWidth = 0f;

        var top = BuildFrameContainer("Frame1_TopRight", rightColGo.transform, out frame1);
        var topLe = top.gameObject.AddComponent<LayoutElement>();
        topLe.flexibleHeight = 1f;
        topLe.minHeight = 0f;

        var bottom = BuildFrameContainer("Frame2_BottomRight", rightColGo.transform, out frame2);
        var bottomLe = bottom.gameObject.AddComponent<LayoutElement>();
        bottomLe.flexibleHeight = 1f;
        bottomLe.minHeight = 0f;
    }

    private bool HasValidBuiltLayout()
    {
        return frame0 != null && frame0.root != null &&
               frame1 != null && frame1.root != null &&
               frame2 != null && frame2.root != null;
    }

    private void CleanupDuplicateGeneratedLayout()
    {
        if (contentRoot == null) return;

        Transform rowToKeep = null;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            var child = contentRoot.GetChild(i);
            if (child.name != "Row") continue;

            if (rowToKeep == null)
            {
                rowToKeep = child;
                continue;
            }

            DestroyGeneratedObject(child.gameObject);
        }

        if (rowToKeep == null)
        {
            frame0 = null;
            frame1 = null;
            frame2 = null;
            return;
        }

        var left = rowToKeep.Find("Frame0_BigLeft");
        var right = rowToKeep.Find("RightColumn");
        var top = right != null ? right.Find("Frame1_TopRight") : null;
        var bottom = right != null ? right.Find("Frame2_BottomRight") : null;

        frame0 = CaptureExistingSlot(left);
        frame1 = CaptureExistingSlot(top);
        frame2 = CaptureExistingSlot(bottom);
    }

    private static FrameSlot CaptureExistingSlot(Transform root)
    {
        if (root == null) return null;

        var slot = new FrameSlot();
        slot.root = root as RectTransform;
        slot.group = root.GetComponent<CanvasGroup>();

        var sprite = root.Find("Sprite");
        if (sprite != null)
        {
            slot.spriteRoot = sprite as RectTransform;
            slot.image = sprite.GetComponent<Image>();
        }

        var plate = root.Find("TextPlate");
        if (plate != null)
        {
            slot.textPlateRoot = plate as RectTransform;
            slot.textGroup = plate.GetComponent<CanvasGroup>();
            slot.textPlateBackground = plate.GetComponent<Image>();
            var text = plate.Find("Text");
            if (text != null)
            {
                slot.text = text.GetComponent<TMP_Text>();
            }
        }

        return slot;
    }

    private static void DestroyGeneratedObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private RectTransform BuildFrameContainer(string name, Transform parent, out FrameSlot slot)
    {
        slot = new FrameSlot();

        var rootGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(RectMask2D));
        rootGo.transform.SetParent(parent, false);
        var rootRt = rootGo.GetComponent<RectTransform>();

        var rootImage = rootGo.GetComponent<Image>();
        rootImage.color = Color.black;
        rootImage.raycastTarget = false;
        rootImage.preserveAspect = false;

        var spriteGo = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
        spriteGo.transform.SetParent(rootGo.transform, false);
        var spriteRt = spriteGo.GetComponent<RectTransform>();
        spriteRt.anchorMin = Vector2.zero;
        spriteRt.anchorMax = Vector2.one;
        spriteRt.offsetMin = new Vector2(frameBorderPixels, frameBorderPixels);
        spriteRt.offsetMax = new Vector2(-frameBorderPixels, -frameBorderPixels);
        var spriteImage = spriteGo.GetComponent<Image>();
        spriteImage.color = Color.white;
        spriteImage.raycastTarget = false;
        spriteImage.preserveAspect = false;
        var fitter = spriteGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1f;

        var group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        var plateGo = new GameObject("TextPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        plateGo.transform.SetParent(rootGo.transform, false);
        var plateRt = plateGo.GetComponent<RectTransform>();
        plateRt.anchorMin = new Vector2(0f, 0f);
        plateRt.anchorMax = new Vector2(1f, 0f);
        plateRt.pivot = new Vector2(0.5f, 0f);
        plateRt.sizeDelta = new Vector2(0f, 140f);
        plateRt.anchoredPosition = new Vector2(0f, 0f);

        var plateBg = plateGo.GetComponent<Image>();
        plateBg.color = new Color(0f, 0f, 0f, textPlateAlpha);
        plateBg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(plateGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(24f, 16f);
        textRt.offsetMax = new Vector2(-24f, -16f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = string.Empty;
        tmp.fontSize = 36f;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        var textGroup = plateGo.GetComponent<CanvasGroup>();
        textGroup.alpha = 0f;

        slot.root = rootRt;
        slot.group = group;
        slot.image = spriteImage;
        slot.spriteRoot = spriteRt;
        slot.textPlateRoot = plateRt;
        slot.textGroup = textGroup;
        slot.textPlateBackground = plateBg;
        slot.text = tmp;

        plateGo.SetActive(false);
        plateGo.transform.SetAsLastSibling();

        return rootRt;
    }

    private static void ApplyBorder(FrameSlot slot)
    {
        if (slot == null) return;
        if (slot.spriteRoot == null) return;
        var owner = slot.spriteRoot.GetComponentInParent<ComicCutsceneUI>();
        int border = 0;
        if (owner != null) border = Mathf.Max(0, owner.frameBorderPixels);
        var min = new Vector2(border, border);
        var max = new Vector2(-border, -border);
        if (slot.spriteRoot.offsetMin != min) slot.spriteRoot.offsetMin = min;
        if (slot.spriteRoot.offsetMax != max) slot.spriteRoot.offsetMax = max;
    }

    private static void ApplyTextPlateStyle(FrameSlot slot)
    {
        if (slot == null) return;
        if (slot.textPlateBackground == null) return;
        var owner = slot.textPlateBackground.GetComponentInParent<ComicCutsceneUI>();
        float a = 0.85f;
        if (owner != null) a = Mathf.Clamp01(owner.textPlateAlpha);
        var c = slot.textPlateBackground.color;
        var target = new Color(0f, 0f, 0f, a);
        if (c != target) slot.textPlateBackground.color = target;
    }

    private static void ApplyTextStyle(FrameSlot slot)
    {
        if (slot == null) return;
        if (slot.text == null) return;

        var owner = slot.text.GetComponentInParent<ComicCutsceneUI>();
        if (owner == null) return;

        owner.CaptureDefaultsIfNeeded();

        var font = owner.frameTextFontOverride != null ? owner.frameTextFontOverride : owner._defaultFrameTextFont;
        if (slot.text.font != font) slot.text.font = font;

        float size = owner.frameTextFontSizeOverride > 0f ? owner.frameTextFontSizeOverride : owner._defaultFrameTextFontSize;
        if (!Mathf.Approximately(slot.text.fontSize, size)) slot.text.fontSize = size;
    }

    private void CaptureDefaultsIfNeeded()
    {
        if (_defaultsCaptured) return;

        TMP_Text t = null;
        if (frame0 != null && frame0.text != null) t = frame0.text;
        else if (frame1 != null && frame1.text != null) t = frame1.text;
        else if (frame2 != null && frame2.text != null) t = frame2.text;

        if (t == null) return;

        _defaultFrameTextFont = t.font;
        _defaultFrameTextFontSize = t.fontSize;
        _defaultsCaptured = true;
    }

    private void PushAnimation()
    {
        _animationCount++;
        _isAnimating = true;
    }

    private void PopAnimation()
    {
        _animationCount = Mathf.Max(0, _animationCount - 1);
        _isAnimating = _animationCount > 0;
    }

    private void BuildSkipButtonIfMissing()
    {
        CleanupDuplicateSkipButtons();
        if (skipButton != null) return;

        var existing = rootCanvas != null ? rootCanvas.transform.Find("SkipButton") : null;
        if (existing != null)
        {
            skipButton = existing.GetComponent<Button>();
            var existingText = existing.Find("Text");
            if (existingText != null) skipButtonText = existingText.GetComponent<TMP_Text>();
            if (skipButton != null) return;
        }

        var btnGo = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(rootCanvas.transform, false);
        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-32f, -32f);
        rt.sizeDelta = new Vector2(220f, 80f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = true;

        skipButton = btnGo.GetComponent<Button>();

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 8f);
        textRt.offsetMax = new Vector2(-12f, -8f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "Пропустить";
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        skipButtonText = tmp;
    }

    private void CleanupDuplicateSkipButtons()
    {
        if (rootCanvas == null) return;

        Button keep = null;
        for (int i = rootCanvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = rootCanvas.transform.GetChild(i);
            if (child.name != "SkipButton") continue;

            var button = child.GetComponent<Button>();
            if (keep == null && button != null)
            {
                keep = button;
                continue;
            }

            DestroyGeneratedObject(child.gameObject);
        }

        if (keep != null)
        {
            skipButton = keep;
            var text = keep.transform.Find("Text");
            if (text != null) skipButtonText = text.GetComponent<TMP_Text>();
        }
    }
}
