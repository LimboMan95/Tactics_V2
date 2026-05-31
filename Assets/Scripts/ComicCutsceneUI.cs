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

    public void Play(ComicSequence[] sequences, int nextSceneBuildIndex, bool loadSceneOnFinish)
    {
        if (sequences == null || sequences.Length == 0) return;
        if (_isActive) return;

        _sequenceQueue = sequences;
        _sequenceIndex = 0;
        _pageIndex = 0;
        _revealIndex = 0;
        _nextSceneBuildIndex = nextSceneBuildIndex;
        _loadSceneOnFinish = loadSceneOnFinish;

        _isActive = true;
        gameObject.SetActive(true);

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

        HidePageImmediate();

        if (screenGroup != null) screenGroup.alpha = 0f;
        gameObject.SetActive(false);
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

    private FrameSlot GetSlot(int index)
    {
        if (index == 0) return frame0;
        if (index == 1) return frame1;
        return frame2;
    }

    private static void ApplyFrameToSlot(FrameSlot slot, ComicFrame frame)
    {
        if (slot == null) return;

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
        if (frame0 == null) frame0 = new FrameSlot();
        if (frame1 == null) frame1 = new FrameSlot();
        if (frame2 == null) frame2 = new FrameSlot();

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
        if (skipButton != null) return;

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
}
