using System.Collections;
using System.Collections.Generic;
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
        public ComicPreviewFrameProxy previewProxy;
        public RectTransform textPlateRoot;
        public CanvasGroup textGroup;
        public Image textPlateBackground;
        public TMP_Text text;
    }

    [Header("Optional Auto-Build")]
    public bool buildIfMissingInPlayMode = true;

    [Header("Visual")]
    [Min(0)] public int frameBorderPixels = 4;
    public TMP_FontAsset frameTextFontOverride;
    [Min(0f)] public float frameTextFontSizeOverride = 0f;

    [Header("Text Plate (Global for All Frames)")]
    [Range(0f, 1f)] public float textPlateAlpha = 0.85f;
    [Min(40f)] public float textPlateHeight = 140f;
    [Min(10f)] public float textPlateFontSize = 36f;
    public Vector2 textPlatePadding = new Vector2(24f, 16f); // X = left/right, Y = top/bottom

    [Header("QTE / Status Layout Sizes")]
    [Min(0f)] public float contentSidePaddingPx = 60f;
    [Min(0f)] public float contentPreferredWidthPx = 1800f;
    [Min(0f)] public float contentMinWidthPx = 1280f;
    [Min(0f)] public float statusHeaderHeightPx = 110f;
    [Min(0f)] public float statusHeaderTopPaddingPx = 24f;
    [Min(0f)] public float statusHeaderHorizontalPaddingPx = 40f;
    [Min(0f)] public float qteBottomHeightPx = 300f;
    [Min(0f)] public float qteBottomBottomPaddingPx = 28f;
    [Min(0f)] public float qteBottomHorizontalPaddingPx = 40f;

    [Header("Matrix Size (Critical for Layout)")]
    [Tooltip("1.0 = матрица вписана ровно в доступную высоту (рекомендовано 1.0-1.15). >1 = крупнее (края кадров обрезаются), <1 = мельче.")]
    [Range(0.5f, 1.8f)] public float matrixZoom = 1.1f;
    [Tooltip("0.1…1.0. Какая ДОЛЯ доступной вертикали отводится под матрицу кадров. 0.95 = 95% (дефолт). 1.0 = под завязку. Позволяет отдельно управлять ВЫСОТОЙ матрицы — НЕ ЗАВИСИМО от того насколько широкий Frame0 или правая колонка.")]
    [Range(0.1f, 1.0f)] public float matrixHeightPercentOfAvailable = 0.95f;
    [Tooltip("Вес ширины Frame0 (большой кадр слева) относительно RightColumn. Дефолт 3 = 60% (при Right=2). Поставь 2 или 1.5 чтобы Frame0 стал уже, а правая колонка шире.")]
    [Min(0.1f)] public float matrixFrame0Weight = 3f;
    [Tooltip("Вес ширины RightColumn (Frame1+Frame2) относительно Frame0. Дефолт 2 = 40% (при Frame0=3). Поставь 2.5 или 3 чтобы два маленьких справа стали шире.")]
    [Min(0.1f)] public float matrixRightColumnWeight = 2f;
    [Tooltip("Spacing между Frame0 и RightColumn")]
    [Min(0f)] public float matrixHorizontalSpacingPx = 14f;
    [Tooltip("Spacing между Frame1 и Frame2 внутри RightColumn")]
    [Min(0f)] public float matrixVerticalSpacingPx = 10f;

    [Header("Game Over / Ending")]
    public int mainMenuSceneBuildIndex = 0;
    [Min(0f)] public float gameOverDelaySeconds = 1.8f;

    [Header("Layout References")]
    public Canvas rootCanvas;
    public CanvasScaler canvasScaler;
    public GraphicRaycaster raycaster;
    public CanvasGroup screenGroup;
    public Image dimBackground;
    public RectTransform contentRoot;
    public RectTransform statusHeaderRoot;
    public CanvasGroup statusHeaderGroup;
    public Image statusHeaderBackground;
    public HorizontalLayoutGroup statusHeaderPillsRow;
    public TMP_Text statusHeaderBannerText;
    public RectTransform qteBottomRoot;
    public CanvasGroup qteBottomGroup;
    public Image qteBottomBackground;
    public TMP_Text qteQuestionText;
    public Image qteTimerFill;
    public TMP_Text qteTimerText;
    public GridLayoutGroup qteOptionsGrid;
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
    private ComicSequence _previewSequence;
    private int _previewPageIndex = -1;
    private readonly List<GameObject> _generatedPills = new List<GameObject>();
    private readonly List<GameObject> _generatedOptions = new List<GameObject>();
    private bool _waitingForQteChoice;
    private Coroutine _timerRoutine;
    private Coroutine _bannerRoutine;
    private int _postQtePendingPage = -2; // -2 = no pending, -1 = invalid/end, >=0 = page
    private int _postQtePendingSequence = -2; // -2 = no pending, -1 = invalid/end, >=0 = sequence
    private bool _isForceRebuilding;

    public bool IsRuntimeShowing
    {
        get
        {
            if (_isPreviewMode) return false;
            return _isActive || _isAnimating || (rootCanvas != null && rootCanvas.enabled);
        }
    }

    // ✅ Unity вызывает OnValidate() АВТОМАТИЧЕСКИ каждый раз когда ты меняешь ЛЮБОЕ поле в инспекторе!
    // Поэтому мы тут сразу пересобираем MatrixGridAlignment — меняешь Frame0Weight, сразу видишь результат!
    private void OnValidate()
    {
        if (this == null) return;
        if (Application.isPlaying) return;

        // Отложенный вызов (следующий кадр Editor) — чтобы не было "SendMessage cannot be called during Awake"
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (Application.isPlaying) return;
            try
            {
                if (!gameObject.activeInHierarchy) return;
                if (contentRoot == null) BuildIfMissing();
                if (frame0.root != null || frame1.root != null || frame2.root != null || contentRoot != null)
                {
                    // ✅ EnsureMatrixGridAlignment ВНУТРИ делает 3 пасса ForceRebuild от детей к родителям.
                    EnsureMatrixGridAlignment();
                }
            }
            catch (System.Exception) { /* В редакторе игнорируем ошибки валидации (объекты могут быть не созданы) */ }
        };
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        ForceRuntimeReset();
    }

    public void Initialize(float fadeDuration, bool skipHideImmediate = false)
    {
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        LogLayout("👉 Initialize() START — before BuildIfMissing");

        if (Application.isPlaying && buildIfMissingInPlayMode)
        {
            BuildIfMissing();
        }
        if (!Application.isPlaying)
        {
            BuildIfMissing();
        }
        LogLayout("👉 Initialize() AFTER BuildIfMissing");
        CaptureDefaultsIfNeeded();
        RefreshBorders();

        EnsureLayoutRootsAlwaysActive();

        ApplyTextPlateGlobalSettingsToAllSlots();

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
        }

        if (!skipHideImmediate)
        {
            HideImmediate();
        }
        LogLayout("👉 Initialize() END");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEBUG")]
    private void LogLayout(string stageLabel)
    {
        if (contentRoot == null)
        {
            Debug.Log($"[LAYOUT {stageLabel}] contentRoot == null");
            return;
        }
        int n = contentRoot.childCount;
        string dump = $"[LAYOUT {stageLabel}] Content children = {n}:";
        System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
        int statusN = 0, rowN = 0, qteN = 0, skipN = 0;
        for (int i = 0; i < n; i++)
        {
            var c = contentRoot.GetChild(i);
            if (c == null) { names.Add($"#{i} NULL"); continue; }
            string name = c.gameObject.name;
            if (name == "StatusHeader") statusN++;
            else if (name == "Row") rowN++;
            else if (name == "QteBottom") qteN++;
            else if (name == "SkipButton") skipN++;
            names.Add($"#{i} [{c.GetSiblingIndex()}] {name}");
        }
        dump += $" Status={statusN}, Row={rowN}, Qte={qteN}, Skip={skipN}, Other={n - statusN - rowN - qteN - skipN}";
        dump += "\n  • " + string.Join("\n  • ", names);
        Debug.Log(dump, this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEBUG")]
    private void LogDeepDebug(string stageLabel)
    {
        string s = $"[DEEP {stageLabel}]";
        if (rootCanvas != null)
        {
            s += $"\n  • canvas.enabled={rootCanvas.enabled}, sortOrder={rootCanvas.sortingOrder}, mode={rootCanvas.renderMode}";
            var rt = rootCanvas.transform as RectTransform;
            if (rt != null) s += $", sizeDelta={rt.sizeDelta}, scale={rt.localScale}, anchor={rt.anchorMin}/{rt.anchorMax}";
        }
        if (screenGroup != null)
        {
            s += $"\n  • screenGroup.alpha={screenGroup.alpha:F3}, interactable={screenGroup.interactable}, blocks={screenGroup.blocksRaycasts}";
        }
        if (contentRoot != null)
        {
            s += $"\n  • contentRoot rect: {contentRoot.rect.width:F2}×{contentRoot.rect.height:F2}";
            Transform rowT = contentRoot.Find("Row");
            if (rowT != null && rowT is RectTransform rowRT)
            {
                s += $"\n  • Row rect: {rowRT.rect.width:F2}×{rowRT.rect.height:F2}";
                Transform rcT = rowRT.Find("RightColumn");
                if (rcT != null && rcT is RectTransform rcRT)
                {
                    s += $"\n  • RightColumn rect: {rcRT.rect.width:F2}×{rcRT.rect.height:F2}";
                }
            }
        }
        var names = new[] { "Frame0", "Frame1", "Frame2" };
        var slots = new[] { frame0, frame1, frame2 };
        for (int i = 0; i < 3; i++)
        {
            var slot = slots[i];
            if (slot == null)
            {
                s += $"\n  • {names[i]}: SLOT NULL";
                continue;
            }
            string active = slot.root != null ? $"rootGO.activeSelf={slot.root.gameObject.activeSelf}" : "root=NULL";
            string alpha = slot.group != null ? $"alpha={slot.group.alpha:F3}" : "group=NULL";
            string img = slot.image != null
                ? $"img.enabled={slot.image.enabled}, sprite={(slot.image.sprite != null ? slot.image.sprite.name : "<NULL>")}, color=({slot.image.color.r:F2},{slot.image.color.g:F2},{slot.image.color.b:F2},{slot.image.color.a:F2}), type={slot.image.type}, preserveAspect={slot.image.preserveAspect}, raycastTarget={slot.image.raycastTarget}"
                : "img=NULL";
            string sp = "";
            if (slot.spriteRoot != null)
            {
                sp = $"spriteRoot.sizeDelta={slot.spriteRoot.sizeDelta}, anchor={slot.spriteRoot.anchorMin}/{slot.spriteRoot.anchorMax}, anchoredPos={slot.spriteRoot.anchoredPosition}, scale={slot.spriteRoot.localScale}";
            }
            else sp = "spriteRoot=NULL";
            string le = "";
            if (slot.root != null)
            {
                var lel = slot.root.GetComponent<LayoutElement>();
                if (lel != null) le = $" LE[prefW={lel.preferredWidth}, prefH={lel.preferredHeight}, flexW={lel.flexibleWidth}, flexH={lel.flexibleHeight}, minW={lel.minWidth}, minH={lel.minHeight}, ignore={lel.ignoreLayout}]";
                else le = " (no LayoutElement)";
            }
            string rectS = "";
            if (slot.root != null)
            {
                rectS = $", rect={slot.root.rect.width:F2}×{slot.root.rect.height:F2}";
            }
            s += $"\n  • {names[i]}: {active} | {alpha} | {img}{rectS}\n       {sp}{le}";
        }
        s += "\n  ---";
        Debug.Log(s, this);
    }

    private void EnsureLayoutRootsAlwaysActive()
    {
        if (statusHeaderRoot != null && !statusHeaderRoot.gameObject.activeSelf)
            statusHeaderRoot.gameObject.SetActive(true);
        if (qteBottomRoot != null && !qteBottomRoot.gameObject.activeSelf)
            qteBottomRoot.gameObject.SetActive(true);
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
        _waitingForQteChoice = false;
        if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        _timerRoutine = null;
        if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
        _bannerRoutine = null;

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
        EnsureLayoutRootsAlwaysActive();
    }

    [ContextMenu("Comic/Force Rebuild Layout (destroy old)")]
    public void ForceRebuildLayoutDestroyAllOld()
    {
        _isForceRebuilding = true;
        try
        {
            List<GameObject> toDestroy = new List<GameObject>();
            if (rootCanvas != null)
            {
                CollectAllByName(rootCanvas.transform, "StatusHeader", toDestroy);
                CollectAllByName(rootCanvas.transform, "QteBottom", toDestroy);
                CollectAllByName(rootCanvas.transform, "Row", toDestroy);
            }
            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                {
                    var c = contentRoot.GetChild(i);
                    if (c != null && (c.name == "Row" || c.name == "StatusHeader" || c.name == "QteBottom"))
                    {
                        if (!toDestroy.Contains(c.gameObject)) toDestroy.Add(c.gameObject);
                    }
                }
            }
            for (int i = toDestroy.Count - 1; i >= 0; i--)
            {
                var go = toDestroy[i];
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go, false);
            }

            frame0 = null;
            frame1 = null;
            frame2 = null;
            statusHeaderRoot = null;
            qteBottomRoot = null;
            statusHeaderGroup = null;
            qteBottomGroup = null;
            statusHeaderBackground = null;
            qteBottomBackground = null;
            statusHeaderBannerText = null;
            qteQuestionText = null;
            qteTimerFill = null;
            qteTimerText = null;
            statusHeaderPillsRow = null;
            qteOptionsGrid = null;

            BuildIfMissing();
            HideStatusHeader();
            HideQteBottom();
            EnsureLayoutRootsAlwaysActive();

            if (rootCanvas != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootCanvas.transform as RectTransform);
            if (contentRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ComicCutsceneUI] ForceRebuildLayout failed: {e.Message}\n{e.StackTrace}", this);
        }
        finally
        {
            _isForceRebuilding = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
    }

    private static void CollectAllByName(Transform root, string name, List<GameObject> result)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var ch = root.GetChild(i);
            if (ch == null) continue;
            if (ch.name == name) result.Add(ch.gameObject);
            CollectAllByName(ch, name, result);
        }
    }

    public void RebuildLayoutNow()
    {
        ForceRebuildLayoutDestroyAllOld();
    }

    public void ShowPreviewPage(ComicPage page, int visibleFrames)
    {
        EnsureBuiltForPreview();

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SetUiVisible(true);
        Initialize(0f, skipHideImmediate: true);
        // ✅ [PREVIEW ONLY] В СЦЕНЕ LevelMenu могли сохраниться СТАРЫЕ сломанные StatusHeader/QteBottom объекты
        // с неправильным порядком детей (Таймер МЕЖДУ кнопками!) или GridLayout 2 колонки вместо 3.
        // УДАЛЯЕМ ИХ ПЕРЕД BUILD, чтобы пересоздать ЧИСТЫЕ, точно как в игре!
        if (contentRoot != null)
        {
            var oldStatus = contentRoot.Find("StatusHeader");
            if (oldStatus != null) DestroyImmediate(oldStatus.gameObject);
            statusHeaderRoot = null; statusHeaderGroup = null; statusHeaderBackground = null;
            statusHeaderBannerText = null; statusHeaderPillsRow = null;

            var oldQte = contentRoot.Find("QteBottom");
            if (oldQte != null) DestroyImmediate(oldQte.gameObject);
            qteBottomRoot = null; qteBottomGroup = null; qteBottomBackground = null;
            qteQuestionText = null; qteTimerFill = null; qteTimerText = null; qteOptionsGrid = null;
        }
        // ✅ ОБЯЗАТЕЛЬНО перед BuildLayout/Apply Qte!
        // Без этого StatusHeader/Qte могли не существовать или быть из старой сцены
        // с неправильными LayoutGroup (кнопки QTE были столбиком вместо 3 колонок!)
        EnsureStatusHeaderAndQteRootsBuilt();
        PrepareLayoutForFrameTransforms();
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

        var state = StoryRuntimeState.Instance;
        var f0Preview = page.frame0.Resolved(state);
        var f1Preview = page.frame1.Resolved(state);
        var f2Preview = page.frame2.Resolved(state);
        ApplyFrameToSlot(frame0, f0Preview, 0);
        ApplyFrameToSlot(frame1, f1Preview, 1);
        ApplyFrameToSlot(frame2, f2Preview, 2);

        SetSlotPreview(frame0, f0Preview, visibleFrames >= 1);
        SetSlotPreview(frame1, f1Preview, visibleFrames >= 2);
        SetSlotPreview(frame2, f2Preview, visibleFrames >= 3);

        ApplyStatusHeaderForPage(page, previewMode: true);
        ApplyQteBottomForPage(page, previewMode: true);
        SetLayoutContextPage(page);
        UpdateContentLayoutForSlots();
        PrepareLayoutForFrameTransforms();

        var state2 = StoryRuntimeState.Instance;
        ApplyImageTransform(frame0, f0Preview, 0);
        ApplyImageTransform(frame1, f1Preview, 1);
        ApplyImageTransform(frame2, f2Preview, 2);
        // ✅ Финальный ребилд всей иерархии Layout:
        //    - EnsureMatrixGridAlignment делает 3 пасса Frame→Row→Content (матрица по весам 2×3/1×4)
        //    - Status pills ContentSizeFitter
        //    - QTE Grid 3-кнопочный ColumnCount
        EnsureMatrixGridAlignment();
        if (qteBottomRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(qteBottomRoot);
        if (statusHeaderRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(statusHeaderRoot);
        LogDeepDebug("ShowPreviewPage END (after ApplyImageTransform x3 + Rebuilds)");
    }

    public void ClearPreview()
    {
        HidePageImmediate();
        ClearSlotContent(frame0);
        ClearSlotContent(frame1);
        ClearSlotContent(frame2);
        ClearPreviewBinding();
        HideStatusHeader();
        HideQteBottom();
        _currentLayoutPage = default(ComicPage);
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
        _waitingForQteChoice = false;
        _postQtePendingPage = -2;
        _postQtePendingSequence = -2;
        _currentLayoutPage = default(ComicPage);

        ResetDisplayState(clearContent: true);
        SetUiVisible(true);
        PrepareLayoutForFrameTransforms();

        _isActive = true;

        StartCoroutine(PlayRoutine());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isActive) return;
        if (_isAnimating) return;
        if (_waitingForQteChoice) return;
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
        HideStatusHeader();
        HideQteBottom();

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
        var state = StoryRuntimeState.Instance;

        if (_revealIndex < 3)
        {
            var frame = page.GetFrame(_revealIndex).Resolved(state);
            FrameSlot slot = GetSlot(_revealIndex);
            yield return RevealSlot(slot, frame);
            _revealIndex++;

            int showAfter = page.QteShowAfterFrameIndex;
            if (page.HasQte && showAfter >= 0 && _revealIndex > showAfter)
            {
                yield return EnterQteWait(page);
            }

            yield break;
        }

        int targetPage = -1;
        int targetSequence = -1;
        if (page.TryGetPageJump(out var pJump)) targetPage = pJump;
        if (page.TryGetSequenceJump(out var sJump)) targetSequence = sJump;

        if (targetPage >= 0 && targetPage < seq.pages.Count && targetPage != _pageIndex)
        {
            yield return FadeOutPage();
            _pageIndex = targetPage;
            _revealIndex = 0;
            if (targetSequence >= 0 && targetSequence < _sequenceQueue.Length) _sequenceIndex = targetSequence;
            yield return LoadCurrentPage();
            yield break;
        }

        if (targetSequence >= 0 && targetSequence < _sequenceQueue.Length && targetSequence != _sequenceIndex)
        {
            yield return FadeOutPage();
            _sequenceIndex = targetSequence;
            _pageIndex = targetPage >= 0 && targetPage < _sequenceQueue[targetSequence].pages.Count ? targetPage : 0;
            _revealIndex = 0;
            yield return LoadCurrentPage();
            yield break;
        }

        if (page.isGameOverPage && page.gameOverBehaviour != ComicPageGameOverBehaviour.None)
        {
            yield return GameOverRoutine(page);
            yield break;
        }

        bool hasPendingJump = _postQtePendingPage >= -1 || _postQtePendingSequence >= -1;
        if (hasPendingJump)
        {
            int tPage = _postQtePendingPage;
            int tSeq = _postQtePendingSequence;
            _postQtePendingPage = -2;
            _postQtePendingSequence = -2;

            bool seqValid = tSeq >= 0 && tSeq < _sequenceQueue.Length && tSeq != _sequenceIndex;
            bool pageValidSameSeq = tPage >= 0 && tPage < seq.pages.Count && tPage != _pageIndex;

            if (seqValid)
            {
                yield return FadeOutPage();
                _sequenceIndex = tSeq;
                if (tPage >= 0 && tPage < _sequenceQueue[tSeq].pages.Count)
                    _pageIndex = tPage;
                else
                    _pageIndex = 0;
                _revealIndex = 0;
                yield return LoadCurrentPage();
                yield break;
            }

            if (pageValidSameSeq)
            {
                yield return FadeOutPage();
                _pageIndex = tPage;
                _revealIndex = 0;
                yield return LoadCurrentPage();
                yield break;
            }

            ComicPage pendingPage = page;
            if (tPage >= 0 && tPage < seq.pages.Count) pendingPage = seq.pages[tPage];
            if (pendingPage.isGameOverPage && pendingPage.gameOverBehaviour != ComicPageGameOverBehaviour.None)
            {
                yield return GameOverRoutine(pendingPage);
                yield break;
            }

            yield return FadeOutPage();
            yield return FinishRoutine();
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
        var state = StoryRuntimeState.Instance;
        var f0 = page.frame0.Resolved(state);
        var f1 = page.frame1.Resolved(state);
        var f2 = page.frame2.Resolved(state);
        ApplyFrameToSlot(frame0, f0, 0);
        ApplyFrameToSlot(frame1, f1, 1);
        ApplyFrameToSlot(frame2, f2, 2);

        SetSlotAlpha(frame0, 0f);
        SetSlotAlpha(frame1, 0f);
        SetSlotAlpha(frame2, 0f);

        ApplyStatusHeaderForPage(page, previewMode: false);
        ApplyQteBottomForPage(page, previewMode: false);
        SetLayoutContextPage(page);
        UpdateContentLayoutForSlots();
        PrepareLayoutForFrameTransforms();

        ApplyImageTransform(frame0, f0, 0);
        ApplyImageTransform(frame1, f1, 1);
        ApplyImageTransform(frame2, f2, 2);

        _revealIndex = 0;
        var stateForLoad = StoryRuntimeState.Instance;
        var firstFrame = page.frame0.Resolved(stateForLoad);
        EnsureMatrixGridAlignment();
        PrepareLayoutForFrameTransforms();
        ApplyImageTransform(frame0, f0, 0);
        ApplyImageTransform(frame1, f1, 1);
        ApplyImageTransform(frame2, f2, 2);
        LogDeepDebug("LoadCurrentPage BEFORE RevealSlot(frame0) (post-EnsureGrid)");
        yield return RevealSlot(frame0, firstFrame);
        LogDeepDebug("LoadCurrentPage AFTER RevealSlot(frame0)");
        _revealIndex = 1;

        int showAfterLoad = page.QteShowAfterFrameIndex;
        if (page.HasQte && showAfterLoad >= 0 && _revealIndex > showAfterLoad)
        {
            yield return EnterQteWait(page);
        }
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
            var fitter = slot.spriteRoot != null ? slot.spriteRoot.GetComponent<AspectRatioFitter>() : null;
            if (fitter != null)
            {
                fitter.aspectMode = AspectRatioFitter.AspectMode.None;
                fitter.aspectRatio = 1f;
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

    private IEnumerator EnterQteWait(ComicPage page)
    {
        _waitingForQteChoice = true;
        ShowQteBottom();
        StartQteTimerIfNeeded(page);
        while (_waitingForQteChoice)
        {
            yield return null;
        }
    }

    private void StartQteTimerIfNeeded(ComicPage page)
    {
        if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        _timerRoutine = null;

        if (!page.HasQte) return;
        if (page.qte.timerSeconds <= 0f)
        {
            if (qteTimerFill != null) qteTimerFill.fillAmount = 1f;
            if (qteTimerText != null)
            {
                qteTimerText.text = string.Empty;
                qteTimerText.gameObject.SetActive(false);
            }
            return;
        }

        if (qteTimerText != null) qteTimerText.gameObject.SetActive(false);
        _timerRoutine = StartCoroutine(QteCountdownRoutine(page));
    }

    private IEnumerator QteCountdownRoutine(ComicPage page)
    {
        float total = Mathf.Max(0.01f, page.qte.timerSeconds);
        float remain = total;

        while (remain > 0f && _waitingForQteChoice)
        {
            remain -= Time.unscaledDeltaTime;
            if (remain < 0f) remain = 0f;
            float t = 1f - Mathf.Clamp01(remain / total);
            if (qteTimerFill != null) qteTimerFill.fillAmount = Mathf.Clamp01(1f - t);
            if (qteTimerText != null) qteTimerText.text = remain.ToString("0.0s");
            yield return null;
        }

        if (_waitingForQteChoice)
        {
            string defaultId = page.qte.timerExpiredDefaultOptionId ?? string.Empty;
            int defaultIdx = -1;
            if (!string.IsNullOrEmpty(defaultId))
            {
                for (int i = 0; i < page.qte.options.Count; i++)
                {
                    if (string.Equals(page.qte.options[i].optionId, defaultId, System.StringComparison.Ordinal))
                    {
                        defaultIdx = i;
                        break;
                    }
                }
            }
            if (defaultIdx < 0 && page.qte.options.Count > 0) defaultIdx = page.qte.options.Count - 1;

            if (defaultIdx >= 0)
            {
                HandleQteChoice(page, page.qte.options[defaultIdx]);
            }
            else
            {
                _waitingForQteChoice = false;
            }
        }
    }

    private void HandleQteChoice(ComicPage page, ComicQteOption option)
    {
        if (!_waitingForQteChoice) return;
        _waitingForQteChoice = false;

        if (_timerRoutine != null)
        {
            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
        }

        ComicQteEffects fx = option.effects;
        var state = StoryRuntimeState.Instance;
        int ctBefore = state.CharlotteTrust;
        int ppBefore = state.PatrickPressure;
        int nsBefore = state.NickStress;
        int nwBefore = state.NickWarmth;
        int mnBefore = state.Money;
        state.CharlotteTrust = Mathf.Clamp(state.CharlotteTrust + fx.deltaCharlotteTrust, -100, 100);
        state.PatrickPressure = Mathf.Clamp(state.PatrickPressure + fx.deltaPatrickPressure, 0, 100);
        state.NickStress = Mathf.Clamp(state.NickStress + fx.deltaNickStress, 0, 100);
        state.NickWarmth = Mathf.Clamp(state.NickWarmth + fx.deltaNickWarmth, -100, 100);
        state.Money = Mathf.Max(0, state.Money + fx.deltaMoney);
        if (fx.setS1BlefUsed) state.S1_BlefUsed = true;
        if (fx.setS2PaidPatrick) state.S2_PaidPatrick = true;
        if (fx.setS2ExtraWorkRequired) state.S2_ExtraWorkRequired = true;

        if (!string.IsNullOrEmpty(option.optionId))
        {
            state.chosenOptionIds.Add(option.optionId);
        }

        string consequenceText = BuildConsequenceBannerText(option.effects.statusBannerAfterChoice,
            ctBefore, ppBefore, nsBefore, nwBefore, mnBefore,
            state.CharlotteTrust, state.PatrickPressure, state.NickStress, state.NickWarmth, state.Money);
        if (!string.IsNullOrWhiteSpace(consequenceText))
        {
            SetStatusBannerTemporarily(consequenceText, 2.6f);
        }

        ApplyInlineFrameOverridesForCurrentPage(option.overrideFramesAfterChoice);

        switch (option.afterChoiceAction)
        {
            case QteAfterChoiceAction.ContinueRevealNextFrame:
                StartCoroutine(AdvanceRoutine());
                break;
            case QteAfterChoiceAction.JumpToRevealFrameIndex:
                int idx = Mathf.Clamp(option.jumpToRevealFrameIndex, 0, 2);
                StartCoroutine(AdvanceAfterQteJumpToFrame(idx));
                break;
            case QteAfterChoiceAction.JumpToPageIndex:
            {
                int pageOverride = Mathf.Max(-1, option.nextPageIndexOverride);
                int seqOverride = Mathf.Max(-1, option.nextSequenceIndexOverride);
                bool pageValid = pageOverride >= 0 && pageOverride != _pageIndex;
                bool seqValid = seqOverride >= 0 && seqOverride < _sequenceQueue.Length && seqOverride != _sequenceIndex;
                StartCoroutine(AdvanceAfterQteWithOverride(seqValid ? seqOverride : -1, pageValid ? pageOverride : -1));
                break;
            }
            case QteAfterChoiceAction.JumpToSequenceIndex:
            {
                int seqOverride = Mathf.Max(-1, option.nextSequenceIndexOverride);
                int pageOverride = Mathf.Max(-1, option.nextPageIndexOverride);
                bool seqValid = seqOverride >= 0 && seqOverride < _sequenceQueue.Length && seqOverride != _sequenceIndex;
                StartCoroutine(AdvanceAfterQteWithOverride(seqValid ? seqOverride : -1, pageOverride));
                break;
            }
            case QteAfterChoiceAction.ContinueThenJumpToPageOrSequence:
            {
                _postQtePendingPage = option.nextPageIndexOverride; // allow -1 = invalid (= end) and >=0
                _postQtePendingSequence = option.nextSequenceIndexOverride; // allow -1 and >=0
                StartCoroutine(AdvanceRoutine());
                break;
            }
            default:
                StartCoroutine(AdvanceRoutine());
                break;
        }
    }

    private void ApplyInlineFrameOverridesForCurrentPage(List<ComicFrameInlineOverride> overrides)
    {
        if (overrides == null || overrides.Count == 0) return;
        if (_sequenceQueue == null || _sequenceQueue.Length <= _sequenceIndex) return;
        var seq = _sequenceQueue[_sequenceIndex];
        if (seq == null || seq.pages == null || seq.pages.Count <= _pageIndex) return;

        var page = seq.pages[_pageIndex];
        for (int i = 0; i < overrides.Count; i++)
        {
            var ov = overrides[i];
            int idx = Mathf.Clamp(ov.frameIndex, 0, 2);
            switch (idx)
            {
                case 0:
                    ApplyInlineOverride(ref page.frame0, ov);
                    break;
                case 1:
                    ApplyInlineOverride(ref page.frame1, ov);
                    break;
                case 2:
                    ApplyInlineOverride(ref page.frame2, ov);
                    break;
            }
        }
        seq.pages[_pageIndex] = page;
    }

    private static void ApplyInlineOverride(ref ComicFrame frame, ComicFrameInlineOverride ov)
    {
        if (ov.sprite != null) frame.sprite = ov.sprite;
        if (ov.showTextPlate || !string.IsNullOrWhiteSpace(ov.frameText))
        {
            if (!string.IsNullOrWhiteSpace(ov.frameText))
                frame.frameText = ov.frameText;
            frame.showTextPlate = ov.showTextPlate | !string.IsNullOrWhiteSpace(ov.frameText);
        }
        if (ov.imageOffset != Vector2.zero) frame.imageOffset = ov.imageOffset;
        if (ov.imageScale != Vector2.zero && ov.imageScale != Vector2.one) frame.imageScale = ov.imageScale;
    }

    private IEnumerator AdvanceAfterQteJumpToFrame(int targetFrameIndex)
    {
        if (_sequenceQueue == null || _sequenceQueue.Length <= _sequenceIndex)
        {
            yield return AdvanceRoutine();
            yield break;
        }
        var seq = _sequenceQueue[_sequenceIndex];
        if (seq == null || seq.pages == null || seq.pages.Count <= _pageIndex)
        {
            yield return AdvanceRoutine();
            yield break;
        }

        var page = seq.pages[_pageIndex];
        int target = Mathf.Clamp(targetFrameIndex, 0, 2);

        if (_revealIndex > target)
        {
            yield return AdvanceRoutine();
            yield break;
        }

        var state = StoryRuntimeState.Instance;
        while (_revealIndex < target)
        {
            FrameSlot slot = GetSlot(_revealIndex);
            var frame = page.GetFrame(_revealIndex).Resolved(state);
            yield return RevealSlot(slot, frame);
            _revealIndex++;
        }

        int showAfter = page.QteShowAfterFrameIndex;
        if (page.HasQte && showAfter >= 0 && _revealIndex > showAfter)
        {
            yield return EnterQteWait(page);
            yield break;
        }

        yield return AdvanceRoutine();
    }

    private IEnumerator AdvanceAfterQteWithOverride(int sequenceIndexOverride, int pageIndexOverride)
    {
        var seq = _sequenceQueue != null && _sequenceQueue.Length > _sequenceIndex ? _sequenceQueue[_sequenceIndex] : null;
        if (seq == null)
        {
            yield return FinishRoutine();
            yield break;
        }

        bool seqChange = sequenceIndexOverride >= 0 && sequenceIndexOverride < _sequenceQueue.Length && sequenceIndexOverride != _sequenceIndex;
        bool pageChange = pageIndexOverride >= 0 && pageIndexOverride != _pageIndex;

        if (seqChange || pageChange)
        {
            yield return FadeOutPage();
            if (seqChange)
            {
                _sequenceIndex = sequenceIndexOverride;
                _pageIndex = pageIndexOverride >= 0 ? pageIndexOverride : 0;
            }
            else
            {
                _pageIndex = pageIndexOverride;
            }
            _revealIndex = 0;
            yield return LoadCurrentPage();
            yield break;
        }

        yield return AdvanceRoutine();
    }

    private IEnumerator FadeOutPage()
    {
        PushAnimation();
        yield return FadeCanvasGroup(frame0.group, 0f, _fadeDuration);
        yield return FadeCanvasGroup(frame1.group, 0f, _fadeDuration);
        yield return FadeCanvasGroup(frame2.group, 0f, _fadeDuration);
        HidePageImmediate();
        HideQteBottom();
        PopAnimation();
    }

    private IEnumerator FinishRoutine()
    {
        PushAnimation();
        yield return FadeOutPage();
        HideStatusHeader();

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

    private IEnumerator GameOverRoutine(ComicPage page)
    {
        PushAnimation();

        if (!string.IsNullOrWhiteSpace(page.gameOverBannerText))
        {
            SetStatusBannerTemporarily(page.gameOverBannerText, Mathf.Max(gameOverDelaySeconds, 1.2f));
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, gameOverDelaySeconds));

        yield return FadeOutPage();
        HideStatusHeader();
        HideQteBottom();

        if (page.gameOverBehaviour == ComicPageGameOverBehaviour.ResetSaveAndQuitToMainMenu)
        {
            try
            {
                StorySaveManager.DeleteSave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("GameOver: failed to delete save: " + e.Message);
            }

            var st = StoryRuntimeState.Instance;
            if (st != null)
            {
                Act1Archetype a = st.archetype;
                StoryRuntimeState newState = ScriptableObject.CreateInstance<StoryRuntimeState>();
                newState.name = "StoryRuntimeState_AfterReset";
                newState.ApplyArchetypePreset(a);
                StoryRuntimeState.ReplaceInstance(newState);
            }
        }

        yield return FadeCanvasGroup(screenGroup, 0f, _fadeDuration);
        HideImmediate();
        PopAnimation();

        _isActive = false;
        _loadSceneOnFinish = true;
        _nextSceneBuildIndex = mainMenuSceneBuildIndex;

        int target = mainMenuSceneBuildIndex;
        if (target < 0 || target >= SceneManager.sceneCountInBuildSettings) target = 0;

        if (screenGroup != null) screenGroup.alpha = 1f;
        yield return LoadSceneAsync(target);
        yield return null;
    }

    private void HideImmediate()
    {
        _isActive = false;
        _waitingForQteChoice = false;
        _sequenceQueue = null;
        _sequenceIndex = 0;
        _pageIndex = 0;
        _revealIndex = 0;
        _isPreviewMode = false;
        if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        _timerRoutine = null;
        if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
        _bannerRoutine = null;
        ClearPreviewBinding();

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
        HideStatusHeader();
        HideQteBottom();

        if (clearContent)
        {
            ClearSlotContent(frame0);
            ClearSlotContent(frame1);
            ClearSlotContent(frame2);
            ClearGeneratedStatusPills();
            ClearGeneratedQteOptions();
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
        if (slot.previewProxy != null) slot.previewProxy.ClearBinding();

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

    public void SetPreviewBinding(ComicSequence sequence, int pageIndex)
    {
        _previewSequence = sequence;
        _previewPageIndex = pageIndex;
    }

    public void ClearPreviewBinding()
    {
        _previewSequence = null;
        _previewPageIndex = -1;
        if (frame0 != null && frame0.previewProxy != null) frame0.previewProxy.ClearBinding();
        if (frame1 != null && frame1.previewProxy != null) frame1.previewProxy.ClearBinding();
        if (frame2 != null && frame2.previewProxy != null) frame2.previewProxy.ClearBinding();
    }

    public ComicPreviewFrameProxy GetPreviewFrameProxy(int index)
    {
        var slot = GetSlot(index);
        return slot != null ? slot.previewProxy : null;
    }

    public Transform GetPreviewFocusTarget(int preferredFrameIndex = -1)
    {
        FrameSlot preferred = preferredFrameIndex >= 0 && preferredFrameIndex <= 2 ? GetSlot(preferredFrameIndex) : null;
        Transform target = GetSlotFocusTarget(preferred);
        if (target != null) return target;

        target = GetSlotFocusTarget(frame0);
        if (target != null) return target;

        target = GetSlotFocusTarget(frame1);
        if (target != null) return target;

        target = GetSlotFocusTarget(frame2);
        if (target != null) return target;

        if (contentRoot != null) return contentRoot;
        return transform;
    }

    private void PrepareLayoutForFrameTransforms()
    {
        if (contentRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();
        if (contentRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void ApplyFrameToSlot(FrameSlot slot, ComicFrame frame, int frameIndex)
    {
        if (slot == null) return;
        if (slot.root != null && !slot.root.gameObject.activeSelf) slot.root.gameObject.SetActive(true);

        ApplyBorder(slot);
        ApplyTextPlateStyle(slot);
        ApplyTextStyle(slot);

        if (slot.image != null)
        {
            // ✅ СБРОС ЦВЕТА В БЕЛЫЙ. Критично для мигрированных сцен LevelMenu —
            // Image.color мог остаться (0,0,0,1) = чёрный, Multiply mode делает спрайт чёрным.
            slot.image.color = Color.white;
            slot.image.sprite = frame.sprite;
            slot.image.enabled = frame.sprite != null;
            slot.image.preserveAspect = true;
            ApplyImageTransform(slot, frame, frameIndex);
        }

        bool wantsText = frame.showTextPlate && !string.IsNullOrWhiteSpace(frame.frameText);
        if (slot.textPlateRoot != null) slot.textPlateRoot.gameObject.SetActive(wantsText);
        if (slot.text != null) slot.text.text = wantsText ? frame.frameText : string.Empty;
    }

    private void ApplyImageTransform(FrameSlot slot, ComicFrame frame, int frameIndex)
    {
        if (slot == null || slot.spriteRoot == null) return;

        var fitter = slot.spriteRoot.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            fitter.aspectRatio = 1f;
        }

        slot.spriteRoot.anchorMin = new Vector2(0.5f, 0.5f);
        slot.spriteRoot.anchorMax = new Vector2(0.5f, 0.5f);
        slot.spriteRoot.pivot = new Vector2(0.5f, 0.5f);

        Vector2 coverSize = CalculateCoverSize(slot, frame.sprite);
        Vector2 scale = frame.GetImageScale();
        slot.spriteRoot.anchoredPosition = frame.imageOffset;
        slot.spriteRoot.sizeDelta = new Vector2(
            coverSize.x * scale.x,
            coverSize.y * scale.y);

        bool editablePreview = _isPreviewMode && _previewSequence != null && _previewPageIndex >= 0;
        if (slot.previewProxy != null)
        {
            slot.previewProxy.Configure(_previewSequence, _previewPageIndex, frameIndex, editablePreview, coverSize);
        }
    }

    private Vector2 CalculateCoverSize(FrameSlot slot, Sprite sprite)
    {
        float border = Mathf.Max(0f, frameBorderPixels);
        Vector2 frameSize = slot.root != null ? slot.root.rect.size : Vector2.zero;
        frameSize.x = Mathf.Max(1f, frameSize.x - border * 2f);
        frameSize.y = Mathf.Max(1f, frameSize.y - border * 2f);

        if (sprite == null)
            return frameSize;

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return frameSize;

        float scale = Mathf.Max(frameSize.x / spriteRect.width, frameSize.y / spriteRect.height);
        return new Vector2(spriteRect.width * scale, spriteRect.height * scale);
    }

    private static Transform GetSlotFocusTarget(FrameSlot slot)
    {
        if (slot == null || slot.root == null || !slot.root.gameObject.activeInHierarchy)
            return null;

        if (slot.previewProxy != null && slot.previewProxy.gameObject.activeInHierarchy)
            return slot.previewProxy.transform;

        if (slot.spriteRoot != null && slot.spriteRoot.gameObject.activeInHierarchy)
            return slot.spriteRoot;

        return slot.root;
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

    private static void NormalizeRectTransform(RectTransform rt,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null,
        Vector2? anchoredPos = null,
        Vector2? sizeDelta = null,
        Vector3? localScale = null,
        Vector3? localPos = null)
    {
        if (rt == null) return;
        if (anchorMin.HasValue) rt.anchorMin = anchorMin.Value;
        if (anchorMax.HasValue) rt.anchorMax = anchorMax.Value;
        if (pivot.HasValue) rt.pivot = pivot.Value;
        if (anchoredPos.HasValue) rt.anchoredPosition = anchoredPos.Value;
        if (sizeDelta.HasValue) rt.sizeDelta = sizeDelta.Value;
        if (localScale.HasValue) rt.localScale = localScale.Value;
        if (localPos.HasValue) rt.localPosition = localPos.Value;
    }

    private void SanitizeBrokenLevelMenuTransforms()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        bool brokenScale =
            Mathf.Abs(rt.localScale.x - 1f) > 0.001f ||
            Mathf.Abs(rt.localScale.y - 1f) > 0.001f ||
            Mathf.Abs(rt.localScale.z - 1f) > 0.001f;
        if (brokenScale)
        {
            rt.localScale = Vector3.one;
        }
        bool brokenRot = Mathf.Abs(rt.localEulerAngles.x) > 0.1f ||
                         Mathf.Abs(rt.localEulerAngles.y) > 0.1f ||
                         Mathf.Abs(rt.localEulerAngles.z) > 0.1f;
        if (brokenRot) rt.localRotation = Quaternion.identity;
    }

    private void BuildIfMissing()
    {
        SanitizeBrokenLevelMenuTransforms();
        if (rootCanvas == null) rootCanvas = GetComponentInChildren<Canvas>(true);
        if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.enabled = true;
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 2000;

        if (canvasScaler == null) canvasScaler = rootCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null) canvasScaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 1.0f;

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
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            contentGo.transform.SetParent(rootCanvas.transform, false);
            contentRoot = contentGo.GetComponent<RectTransform>();
            var vg = contentGo.GetComponent<VerticalLayoutGroup>();
            vg.childControlHeight = true;
            vg.childControlWidth = true;
            vg.childForceExpandHeight = true;
            vg.childForceExpandWidth = true;
            vg.spacing = 0f;
            int padH = Mathf.RoundToInt(contentSidePaddingPx);
            vg.padding = new RectOffset(padH, padH, 0, 0);
            var cle = contentGo.GetComponent<LayoutElement>();
            cle.preferredWidth = contentPreferredWidthPx;
            cle.minWidth = contentMinWidthPx;
            contentRoot.anchorMin = new Vector2(0.5f, 0f);
            contentRoot.anchorMax = new Vector2(0.5f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(contentPreferredWidthPx, 0f);
        }
        else
        {
            // Ensure padding & layout widths on existing content (migration fix).
            var vg = contentRoot.GetComponent<VerticalLayoutGroup>();
            int padH = Mathf.RoundToInt(contentSidePaddingPx);
            if (vg != null && (vg.padding.left != padH || vg.padding.right != padH))
            {
                vg.padding = new RectOffset(padH, padH, vg.padding.top, vg.padding.bottom);
            }
            var cle = contentRoot.GetComponent<LayoutElement>();
            if (cle == null) cle = contentRoot.gameObject.AddComponent<LayoutElement>();
            if (Mathf.Abs(cle.preferredWidth - contentPreferredWidthPx) > 1f) cle.preferredWidth = contentPreferredWidthPx;
            if (Mathf.Abs(cle.minWidth - contentMinWidthPx) > 1f) cle.minWidth = contentMinWidthPx;
            if (contentRoot.anchorMin.x != 0.5f || contentRoot.anchorMax.x != 0.5f)
            {
                contentRoot.anchorMin = new Vector2(0.5f, contentRoot.anchorMin.y);
                contentRoot.anchorMax = new Vector2(0.5f, contentRoot.anchorMax.y);
                contentRoot.pivot = new Vector2(0.5f, contentRoot.pivot.y);
                contentRoot.anchoredPosition = Vector2.zero;
                contentRoot.sizeDelta = new Vector2(contentPreferredWidthPx, contentRoot.sizeDelta.y);
            }
        }

        BuildLayoutIfMissing();
        BuildSkipButtonIfMissing();
    }

    private void BuildLayoutIfMissing()
    {
        if (contentRoot == null) return;

        LogLayout("  ⚙️ BuildLayoutIfMissing → BEFORE Cleanup");
        CleanupDuplicateGeneratedLayout();
        LogLayout("  ⚙️ BuildLayoutIfMissing → BEFORE Normalize");
        NormalizeContentRootChildrenFixed();
        LogLayout("  ⚙️ BuildLayoutIfMissing → AFTER Normalize");

        bool valid = HasValidBuiltLayout();
        Debug.Log($"[LAYOUT] HasValidBuiltLayout = {valid}", this);
        if (valid)
        {
            EnsureStatusHeaderAndQteRootsBuilt();
            ApplyContentLayoutSizes();
            LogLayout("  ✅ BuildLayoutIfMissing: valid, EARLY RETURN (no build)");
            return;
        }

        if (_isForceRebuilding)
        {
            Debug.Log("[LAYOUT] ℹ️  HasValid=false (expected for ForceRebuild) → building NEW layout from scratch.", this);
        }
        else
        {
            Debug.LogWarning("[LAYOUT] ⚠️  HasValid=false (unexpected) → auto-rebuilding layout from scratch. Consider using Force Rebuild Layout manually.", this);
        }
        // First, wipe ALL existing StatusHeader/Row/QteBottom children so we never get duplicate Row.
        System.Collections.Generic.List<GameObject> oldToKill = new System.Collections.Generic.List<GameObject>();
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            var c = contentRoot.GetChild(i);
            if (c == null) continue;
            if (c.name == "StatusHeader" || c.name == "Row" || c.name == "QteBottom")
                oldToKill.Add(c.gameObject);
        }
        for (int i = 0; i < oldToKill.Count; i++)
        {
            var go = oldToKill[i];
            if (go == null) continue;
            Debug.Log($"[LAYOUT]    → destroy old '{go.name}' so we start from clean slate", this);
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go, false);
        }
        frame0 = null;
        frame1 = null;
        frame2 = null;
        statusHeaderRoot = null;
        qteBottomRoot = null;

        EnsureStatusHeaderAndQteRootsBuilt();

        frame0 = new FrameSlot();
        frame1 = new FrameSlot();
        frame2 = new FrameSlot();

        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(contentRoot, false);
        rowGo.transform.SetSiblingIndex(1);
        var row = rowGo.GetComponent<HorizontalLayoutGroup>();
        row.childControlHeight = false;
        row.childControlWidth = true;
        row.childForceExpandHeight = false;
        row.childForceExpandWidth = true;
        row.spacing = matrixHorizontalSpacingPx;
        row.padding = new RectOffset(0, 0, 0, 0);
        row.childAlignment = TextAnchor.UpperLeft;
        var rowLe = rowGo.gameObject.AddComponent<LayoutElement>();
        rowLe.flexibleHeight = 1f;
        rowLe.minHeight = 0f;
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;

        var left = BuildFrameContainer("Frame0_BigLeft", rowGo.transform, out frame0);
        var leftLe = left.gameObject.GetComponent<LayoutElement>();
        if (leftLe != null) leftLe.flexibleWidth = 3f; // 60% на 40%

        var rightColGo = new GameObject("RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rightColGo.transform.SetParent(rowGo.transform, false);
        var rightCol = rightColGo.GetComponent<VerticalLayoutGroup>();
        rightCol.childControlHeight = false;
        rightCol.childControlWidth = true;
        rightCol.childForceExpandHeight = false;
        rightCol.childForceExpandWidth = true;
        rightCol.spacing = matrixVerticalSpacingPx;
        rightCol.padding = new RectOffset(0, 0, 0, 0);
        rightCol.childAlignment = TextAnchor.UpperRight;
        var rightLe = rightColGo.gameObject.AddComponent<LayoutElement>();
        rightLe.flexibleWidth = 2f; // 40% ширины
        rightLe.minWidth = 0f;

        var top = BuildFrameContainer("Frame1_TopRight", rightColGo.transform, out frame1);
        var bottom = BuildFrameContainer("Frame2_BottomRight", rightColGo.transform, out frame2);

        ApplyContentLayoutSizes();
        EnsureMatrixGridAlignment();
    }

    private void EnsureStatusHeaderAndQteRootsBuilt()
    {
        if (contentRoot == null) return;

        if (statusHeaderRoot == null)
        {
            var existing = contentRoot.Find("StatusHeader");
            if (existing != null) CaptureStatusHeader(existing);
        }
        if (statusHeaderRoot == null)
        {
            BuildStatusHeader();
        }
        else if (statusHeaderRoot.parent != contentRoot)
        {
            statusHeaderRoot.SetParent(contentRoot, false);
        }
        statusHeaderRoot.SetSiblingIndex(0);

        if (qteBottomRoot == null)
        {
            var existing = contentRoot.Find("QteBottom");
            if (existing != null) CaptureQteBottom(existing);
        }
        if (qteBottomRoot == null)
        {
            BuildQteBottom();
        }
        else if (qteBottomRoot.parent != contentRoot)
        {
            qteBottomRoot.SetParent(contentRoot, false);
        }

        int last = contentRoot.childCount - 1;
        if (qteBottomRoot.GetSiblingIndex() != last) qteBottomRoot.SetSiblingIndex(last);
    }

    private void CaptureStatusHeader(Transform rt)
    {
        statusHeaderRoot = rt as RectTransform;
        statusHeaderGroup = rt.GetComponent<CanvasGroup>();
        var bg = rt.Find("Background");
        if (bg != null) statusHeaderBackground = bg.GetComponent<Image>();
        var banner = rt.Find("BannerText");
        if (banner != null) statusHeaderBannerText = banner.GetComponent<TMP_Text>();
        var row = rt.Find("PillsRow");
        if (row != null) statusHeaderPillsRow = row.GetComponent<HorizontalLayoutGroup>();
    }

    private void CaptureQteBottom(Transform rt)
    {
        qteBottomRoot = rt as RectTransform;
        qteBottomGroup = rt.GetComponent<CanvasGroup>();
        var bg = rt.Find("Background");
        if (bg != null) qteBottomBackground = bg.GetComponent<Image>();
        var q = rt.Find("QuestionText");
        if (q != null) qteQuestionText = q.GetComponent<TMP_Text>();
        var tf = rt.Find("TimerRow/TimerFill");
        if (tf != null) qteTimerFill = tf.GetComponent<Image>();
        var tt = rt.Find("TimerRow/TimerText");
        if (tt != null) qteTimerText = tt.GetComponent<TMP_Text>();
        var col = rt.Find("OptionsGrid");
        if (col != null) qteOptionsGrid = col.GetComponent<GridLayoutGroup>();
        if (qteOptionsGrid == null)
        {
            var old = rt.Find("OptionsColumn");
            if (old != null) qteOptionsGrid = old.GetComponent<GridLayoutGroup>();
        }
    }

    private void BuildStatusHeader()
    {
        var rootGo = new GameObject("StatusHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        rootGo.transform.SetParent(contentRoot, false);
        statusHeaderRoot = rootGo.GetComponent<RectTransform>();
        statusHeaderGroup = rootGo.GetComponent<CanvasGroup>();
        statusHeaderGroup.alpha = 0f;
        statusHeaderGroup.interactable = false;
        statusHeaderGroup.blocksRaycasts = false;

        statusHeaderBackground = rootGo.GetComponent<Image>();
        statusHeaderBackground.color = new Color(0f, 0f, 0f, 0.72f);
        statusHeaderBackground.raycastTarget = false;

        var inner = new GameObject("Inner", typeof(RectTransform), typeof(VerticalLayoutGroup));
        inner.transform.SetParent(rootGo.transform, false);
        var innerRt = (RectTransform)inner.transform;
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        var innerVl = inner.GetComponent<VerticalLayoutGroup>();
        innerVl.childControlHeight = true;
        innerVl.childControlWidth = true;
        innerVl.childForceExpandHeight = false;
        innerVl.childForceExpandWidth = true;
        innerVl.spacing = 8f;

        var bannerGo = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bannerGo.transform.SetParent(inner.transform, false);
        statusHeaderBannerText = bannerGo.GetComponent<TMP_Text>();
        statusHeaderBannerText.text = string.Empty;
        statusHeaderBannerText.fontSize = 26f;
        statusHeaderBannerText.alignment = TextAlignmentOptions.MidlineLeft;
        statusHeaderBannerText.color = new Color(1f, 0.78f, 0.45f, 1f);
        statusHeaderBannerText.enableWordWrapping = true;
        var bannerLe = bannerGo.AddComponent<LayoutElement>();
        bannerLe.minHeight = 0f;
        bannerLe.flexibleHeight = 0f;

        var rowGo = new GameObject("PillsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        rowGo.transform.SetParent(inner.transform, false);
        statusHeaderPillsRow = rowGo.GetComponent<HorizontalLayoutGroup>();
        statusHeaderPillsRow.childAlignment = TextAnchor.UpperLeft;
        statusHeaderPillsRow.childControlHeight = true;
        statusHeaderPillsRow.childControlWidth = true;
        statusHeaderPillsRow.childForceExpandHeight = true;
        statusHeaderPillsRow.childForceExpandWidth = false;
        statusHeaderPillsRow.spacing = 14f;
        statusHeaderPillsRow.padding = new RectOffset(0, 0, 0, 0);
        var csf = rowGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var le = rootGo.GetComponent<LayoutElement>();
        le.flexibleHeight = 0f;
        le.minHeight = 0f;
        le.preferredHeight = statusHeaderHeightPx;

        rootGo.SetActive(false);
    }

    private void BuildQteBottom()
    {
        var rootGo = new GameObject("QteBottom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        rootGo.transform.SetParent(contentRoot, false);
        qteBottomRoot = rootGo.GetComponent<RectTransform>();
        qteBottomGroup = rootGo.GetComponent<CanvasGroup>();
        qteBottomGroup.alpha = 0f;
        qteBottomGroup.interactable = true;
        qteBottomGroup.blocksRaycasts = true;

        qteBottomBackground = rootGo.GetComponent<Image>();
        qteBottomBackground.color = new Color(0f, 0f, 0f, 0.78f);
        qteBottomBackground.raycastTarget = false;

        var inner = new GameObject("Inner", typeof(RectTransform), typeof(VerticalLayoutGroup));
        inner.transform.SetParent(rootGo.transform, false);
        var innerRt = (RectTransform)inner.transform;
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        var innerVl = inner.GetComponent<VerticalLayoutGroup>();
        innerVl.childControlHeight = true;
        innerVl.childControlWidth = true;
        innerVl.childForceExpandHeight = false;
        innerVl.childForceExpandWidth = true;
        innerVl.spacing = 12f;

        var questionGo = new GameObject("QuestionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        questionGo.transform.SetParent(inner.transform, false);
        qteQuestionText = questionGo.GetComponent<TMP_Text>();
        qteQuestionText.text = string.Empty;
        qteQuestionText.fontSize = 30f;
        qteQuestionText.alignment = TextAlignmentOptions.MidlineLeft;
        qteQuestionText.color = Color.white;
        qteQuestionText.enableWordWrapping = true;
        var qLe = questionGo.AddComponent<LayoutElement>();
        qLe.minHeight = 0f;
        qLe.flexibleHeight = 0f;

        var timerCenterGo = new GameObject("TimerCenter", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        timerCenterGo.transform.SetParent(inner.transform, false);
        var tcVl = timerCenterGo.GetComponent<HorizontalLayoutGroup>();
        tcVl.childControlHeight = true;
        tcVl.childControlWidth = false;        // центрируем TimerRow по горизонтали
        tcVl.childForceExpandHeight = false;
        tcVl.childForceExpandWidth = false;
        tcVl.childAlignment = TextAnchor.MiddleCenter;
        tcVl.spacing = 0f;
        tcVl.padding = new RectOffset(0, 0, 0, 0);
        var tcLe = timerCenterGo.GetComponent<LayoutElement>();
        tcLe.minHeight = 0f;
        tcLe.flexibleHeight = 0f;
        tcLe.flexibleWidth = 1f; // занимаем всю ширину, но внутри по центру ставим узкий TimerRow

        var timerRowGo = new GameObject("TimerRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        timerRowGo.transform.SetParent(timerCenterGo.transform, false);
        var tRow = timerRowGo.GetComponent<HorizontalLayoutGroup>();
        tRow.childControlHeight = true;
        tRow.childControlWidth = true;
        tRow.childForceExpandHeight = true;
        tRow.childForceExpandWidth = true;
        tRow.spacing = 14f;
        tRow.padding = new RectOffset(0, 0, 0, 0);
        tRow.childAlignment = TextAnchor.MiddleCenter;
        var tle = timerRowGo.GetComponent<LayoutElement>();
        tle.preferredWidth = 760f;   // узкий таймер ~ 760 px, не на всю панель
        tle.minWidth = 520f;
        tle.flexibleWidth = 0f;
        tle.minHeight = 14f;
        tle.preferredHeight = 14f;
        tle.flexibleHeight = 0f;

        var timerBgGo = new GameObject("TimerBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        timerBgGo.transform.SetParent(timerRowGo.transform, false);
        var timerBg = timerBgGo.GetComponent<Image>();
        timerBg.color = new Color(249f / 255f, 211f / 255f, 66f / 255f, 0.95f); // желтый
        timerBg.raycastTarget = false;
        var timerBgRt = (RectTransform)timerBgGo.transform;
        timerBgRt.anchorMin = Vector2.zero;
        timerBgRt.anchorMax = Vector2.one;
        timerBgRt.offsetMin = Vector2.zero;
        timerBgRt.offsetMax = Vector2.zero;
        var bgLe = timerBgGo.AddComponent<LayoutElement>();
        bgLe.flexibleWidth = 1f;
        bgLe.minWidth = 0f;
        bgLe.preferredHeight = 14f;
        bgLe.minHeight = 14f;

        var timerFillGo = new GameObject("TimerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        timerFillGo.transform.SetParent(timerBgGo.transform, false);
        qteTimerFill = timerFillGo.GetComponent<Image>();
        qteTimerFill.color = new Color(1f, 106f / 255f, 106f / 255f, 1f); // красный
        qteTimerFill.raycastTarget = false;
        qteTimerFill.type = Image.Type.Filled;
        qteTimerFill.fillMethod = Image.FillMethod.Horizontal;
        qteTimerFill.fillOrigin = (int)Image.OriginHorizontal.Right;
        qteTimerFill.fillAmount = 1f;
        var tfRt = (RectTransform)timerFillGo.transform;
        tfRt.anchorMin = new Vector2(0f, 0f);
        tfRt.anchorMax = new Vector2(1f, 1f);
        tfRt.offsetMin = Vector2.zero;
        tfRt.offsetMax = Vector2.zero;

        var timerTextGo = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        timerTextGo.transform.SetParent(timerRowGo.transform, false);
        qteTimerText = timerTextGo.GetComponent<TMP_Text>();
        qteTimerText.text = string.Empty;
        qteTimerText.fontSize = 24f;
        qteTimerText.alignment = TextAlignmentOptions.MidlineRight;
        qteTimerText.color = new Color(1f, 1f, 1f, 0.9f);
        var tLe = timerTextGo.AddComponent<LayoutElement>();
        tLe.minWidth = 90f;
        tLe.flexibleWidth = 0f;

        var centerGo = new GameObject("OptionsCenter", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        centerGo.transform.SetParent(inner.transform, false);
        var centerRt = (RectTransform)centerGo.transform;
        centerRt.anchorMin = new Vector2(0f, 0.5f);
        centerRt.anchorMax = new Vector2(1f, 0.5f);
        centerRt.sizeDelta = new Vector2(0f, 0f);
        var centerHlg = centerGo.GetComponent<HorizontalLayoutGroup>();
        centerHlg.childAlignment = TextAnchor.MiddleCenter;
        centerHlg.childControlHeight = true;
        centerHlg.childControlWidth = true;
        centerHlg.childForceExpandHeight = false;
        centerHlg.childForceExpandWidth = false;
        centerHlg.spacing = 0f;
        centerHlg.padding = new RectOffset(0, 0, 0, 0);

        var colGo = new GameObject("OptionsGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        colGo.transform.SetParent(centerGo.transform, false);
        qteOptionsGrid = colGo.GetComponent<GridLayoutGroup>();
        qteOptionsGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        qteOptionsGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        qteOptionsGrid.childAlignment = TextAnchor.UpperCenter;
        // ✅ ВСЕГДА 3 КОЛОНКИ, не зависимо от ширины превью/игры!
        // Раньше было Flexible → из-за ширины <1920 в превью превращалось в 2 колонки × 2 ряда
        qteOptionsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        qteOptionsGrid.constraintCount = 3;
        qteOptionsGrid.spacing = new Vector2(16f, 12f);
        qteOptionsGrid.padding = new RectOffset(0, 0, 0, 0);
        // cellSize.x теперь 460 (не 640!) — т.к. 3 × 460 = 1380 + 2×16 = 1412, влезает даже в узкий preview
        // А в игре 1920 ширины — кнопки будут центрированы через centerAlignment=MiddleCenter
        qteOptionsGrid.cellSize = new Vector2(460f, 64f);

        var fitter = colGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var colCenLe = centerGo.AddComponent<LayoutElement>();
        colCenLe.flexibleWidth = 1f;
        colCenLe.flexibleHeight = 0f;
        colCenLe.minHeight = 0f;

        var le = rootGo.GetComponent<LayoutElement>();
        le.flexibleHeight = 0f;
        le.minHeight = 0f;
        le.preferredHeight = qteBottomHeightPx;

        rootGo.SetActive(false);
    }

    [System.NonSerialized] private ComicPage _currentLayoutPage;

    public void SetLayoutContextPage(ComicPage page)
    {
        _currentLayoutPage = page;
    }

    private void ApplyContentLayoutSizes()
    {
        if (statusHeaderRoot != null)
        {
            var le = statusHeaderRoot.GetComponent<LayoutElement>();
            if (le == null) le = statusHeaderRoot.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = Mathf.Max(0f, statusHeaderHeightPx);
            le.flexibleHeight = 0f;
            le.minHeight = 0f;
            le.ignoreLayout = false;

            var vlg = statusHeaderRoot.GetComponentInChildren<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(
                    Mathf.RoundToInt(Mathf.Max(0f, statusHeaderHorizontalPaddingPx)),
                    Mathf.RoundToInt(Mathf.Max(0f, statusHeaderHorizontalPaddingPx)),
                    Mathf.RoundToInt(Mathf.Max(0f, statusHeaderTopPaddingPx)),
                    16);
            }
        }

        if (qteBottomRoot != null)
        {
            var le = qteBottomRoot.GetComponent<LayoutElement>();
            if (le == null) le = qteBottomRoot.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = Mathf.Max(0f, qteBottomHeightPx);
            le.flexibleHeight = 0f;
            le.minHeight = 0f;
            le.ignoreLayout = false;

            var vlg = qteBottomRoot.GetComponentInChildren<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(
                    Mathf.RoundToInt(Mathf.Max(0f, qteBottomHorizontalPaddingPx)),
                    Mathf.RoundToInt(Mathf.Max(0f, qteBottomHorizontalPaddingPx)),
                    20,
                    Mathf.RoundToInt(Mathf.Max(0f, qteBottomBottomPaddingPx)));
            }
        }

        if (contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
    }

    /// <summary>
    /// ФИНАЛЬНАЯ СТРАТЕГИЯ матрицы — сохраняет ВСЁ:
    /// 1. Frame0 60% / RightColumn 40% по ШИРИНЕ контента
    /// 2. Каждый кадр (Frame0/1/2) сохраняет соотношение 16:9 (без чёрных полос letterbox)
    /// 3. matrixZoom = 1.0 → кадры точно вписаны (edges no crop). 1.1…1.2 → кадры крупнее, края обрезаются
    /// 4. Status/QTE ВСЕГДА резервируют место → страницы НЕ ПРЫГАЮТ
    /// </summary>
    private void EnsureMatrixGridAlignment()
    {
        if (contentRoot == null || frame0?.root == null || frame1?.root == null || frame2?.root == null) return;

        float spacingRC = Mathf.Max(0f, matrixVerticalSpacingPx);

        var rightCol = frame1.root.parent;
        if (rightCol == null || rightCol.name != "RightColumn") return;
        var row = rightCol.parent;
        if (row == null || row.name != "Row") return;

        // ⚠️ LEVELMENU HOTFIX: На застарелой сцене SizeDelta.y Frame0=524, Frame1=368, anchors=(0,0).
        // Horizontal/VerticalLayoutGroup НЕ УПРАВЛЯЮТ детьми у которых anchor не stretch или есть sizeDelta.
        // Сбрасываем anchors/position/sizeDelta на «чистый шаблон» перед тем как LayoutElement будет работать.
        NormalizeRectTransform(frame0.root,
            anchoredPos: Vector2.zero,
            sizeDelta: Vector2.zero,
            localScale: Vector3.one,
            localPos: Vector3.zero);
        NormalizeRectTransform(frame1.root,
            anchoredPos: Vector2.zero,
            sizeDelta: Vector2.zero,
            localScale: Vector3.one,
            localPos: Vector3.zero);
        NormalizeRectTransform(frame2.root,
            anchoredPos: Vector2.zero,
            sizeDelta: Vector2.zero,
            localScale: Vector3.one,
            localPos: Vector3.zero);

        var rowHlg = row.GetComponent<HorizontalLayoutGroup>();
        if (rowHlg != null)
        {
            rowHlg.spacing = matrixHorizontalSpacingPx;
            rowHlg.childForceExpandHeight = false;
            rowHlg.childForceExpandWidth = true;
            // ✅ childControlHeight = true ОБЯЗАТЕЛЬНО! Иначе HLG не будет проставлять высоту = LE.preferredHeight
            // Без этого мы видели Frame1/2 prefH=348, но real sizeDelta=128 (оставалось старое)
            rowHlg.childControlHeight = true;
            rowHlg.childControlWidth = true;
            rowHlg.childAlignment = TextAnchor.MiddleCenter;
            rowHlg.padding = new RectOffset(0, 0, 0, 0);
        }

        var rcVlg = rightCol.GetComponent<VerticalLayoutGroup>();
        if (rcVlg != null)
        {
            rcVlg.spacing = spacingRC;
            rcVlg.childForceExpandHeight = false;
            rcVlg.childForceExpandWidth = true;
            rcVlg.childControlHeight = true;  // ✅ аналогично VLG обязан управлять высотой
            rcVlg.childControlWidth = true;
            rcVlg.childAlignment = TextAnchor.MiddleCenter;
            rcVlg.padding = new RectOffset(0, 0, 0, 0);
        }

        var rowAsRect = row as RectTransform;

        // ⚠️ ВАЖНО! ИСПОЛЬЗУЕМ contentPreferredWidthPx (Inspector-настройку), а НЕ contentRoot.rect.width!
        // rect.width — результат последнего layout-пасса Unity (может быть 0 или маленький пока layout не пересчитался).
        // contentPreferredWidthPx — достоверное значение 1800/1880/1920/..., оно всегда актуально.
        float contentPadding = contentRoot.GetComponent<VerticalLayoutGroup>()?.padding.horizontal ?? 0;
        float contentW = Mathf.Max(contentPreferredWidthPx - contentPadding, contentMinWidthPx, contentRoot.rect.width - contentPadding);

        float rowInnerW = Mathf.Max(100f, contentW - matrixHorizontalSpacingPx);

        // ✅ ДИНАМИЧЕСКИЕ ПРОПОРЦИИ ИЗ ИНСПЕКТОРА!
        // Старый хардкод: 3/5 и 2/5 (60/40). Теперь настраивается через matrixFrame0Weight/matrixRightColumnWeight.
        float w0 = Mathf.Max(0.1f, matrixFrame0Weight);
        float w1 = Mathf.Max(0.1f, matrixRightColumnWeight);
        float totalW = w0 + w1;
        float f0IdealW = rowInnerW * w0 / totalW;
        float rcIdealW = rowInnerW * w1 / totalW;

        // ✅ ВЫСОТА МАТРИЦЫ НЕ ЗАВИСИТ ОТ ШИРИНЫ КАДРОВ!
        // Старый баг: maxSafeMatrixH = MIN(Frame0.w×9/16, Right.w×9/16×2+spacing).
        // Если Frame0 становился уже (вес=2 или 1) — ОН СТАНОВИЛСЯ НИЖЕ, вся матрица сжималась!
        // Решение: targetMatrixH = доступная_вертикаль × matrixHeightPercent × matrixZoom.
        // Каждый кадр отдельно cover-вписывается в свою рамку (как и задумано спрайт-системой).
        float inspectorBasedAvailable = Mathf.Max(60f, 1080f - statusHeaderHeightPx - qteBottomHeightPx);
        float rectBasedAvailable = rowAsRect != null ? rowAsRect.rect.height : 0f;
        float contentRectH = contentRoot.rect.height;
        if (contentRectH > 1f)
        {
            float fromContentH = Mathf.Max(60f, contentRectH - statusHeaderHeightPx - qteBottomHeightPx);
            rectBasedAvailable = Mathf.Max(rectBasedAvailable, fromContentH);
        }
        float availableVertical = Mathf.Max(inspectorBasedAvailable * 0.95f, rectBasedAvailable);
        if (availableVertical <= 1f)
        {
            availableVertical = inspectorBasedAvailable;
        }

        // matrixHeightPercentOfAvailable (0.1…1.0, дефолт 0.95)
        //   = сколько процентов доступной вертикали реально отдаём под матрицу
        // matrixZoom (0.5…1.8, дефолт 1.1)
        //   = масштаб ИМЕННО РАМКИ матрицы (и Frame1/Frame2 растянутся вместе с ней)
        // Финальная высота рамки = доступная вертикаль × процент × zoom
        float heightPercent = Mathf.Clamp01(matrixHeightPercentOfAvailable);
        float targetMatrixH = Mathf.Max(120f, availableVertical * heightPercent * Mathf.Max(0.5f, matrixZoom));

        // RightColumn height = targetMatrixH
        var rightLe = rightCol.GetComponent<LayoutElement>();
        if (rightLe == null) rightLe = rightCol.gameObject.AddComponent<LayoutElement>();
        rightLe.preferredHeight = targetMatrixH;
        rightLe.minHeight = targetMatrixH;
        rightLe.flexibleHeight = 0f;

        // Frame1/2 = каждый (targetMatrixH - spacing) / 2 = ровно половина RightColumn
        // ИНВАРИАНТ: 2 × eachRightH + spacingRC === targetMatrixH
        // Гарантирует что Frame1 + spacing + Frame2 НИКОГДА не выйдут за границу RightColumn (исправление Frame2-налезания-на-QTE)
        float eachRightH = Mathf.Max(60f, (targetMatrixH - spacingRC) * 0.5f);
        // Пересчитываем точно по eachRightH, чтобы не было дробных 1-2px перебора
        float finalMatrixH = 2f * eachRightH + spacingRC;
        var rightLeFinal = rightCol.GetComponent<LayoutElement>();
        if (rightLeFinal == null) rightLeFinal = rightCol.gameObject.AddComponent<LayoutElement>();
        rightLeFinal.preferredHeight = finalMatrixH;
        rightLeFinal.minHeight = finalMatrixH;
        rightLeFinal.flexibleHeight = 0f;
        // ✅ ⚠️ КЛЮЧЕВОЙ ИСПРАВЛЕНИЕ: ШИРИНЫ ПО ВЕСАМ, а НЕ 16/9!
        // Раньше было: Frame0.preferredWidth = finalMatrixH * 16/9 (жёстко, игнорирует веса).
        // RightCol вообще preferredWidth не задавался (0). В итоге HLG всегда отдавал 80% ширины Frame0.
        // Теперь: f0IdealW = rowInnerW * w0 / totalW, rcIdealW = rowInnerW * w1 / totalW
        // preferredWidth = ИДЕАЛЬНАЯ ШИРИНА. flexibleWidth=0. Unity HLG childForceExpand=true подгонит ровно 40/60!
        rightLeFinal.preferredWidth = rcIdealW;
        rightLeFinal.minWidth = Mathf.Max(100f, rcIdealW * 0.3f);
        rightLeFinal.flexibleWidth = 0f;

        // Frame0 высоту тоже корректируем под точно такой же finalMatrixH
        var f0le = frame0.root.GetComponent<LayoutElement>();
        if (f0le != null)
        {
            f0le.preferredHeight = finalMatrixH;
            f0le.minHeight = finalMatrixH;
            f0le.flexibleHeight = 0f;
            // ✅ Ширина Frame0 = ИДЕАЛЬНАЯ ПО ВЕСАМ, а не 16:9 × высота!
            f0le.preferredWidth = f0IdealW;
            f0le.minWidth = Mathf.Max(100f, f0IdealW * 0.3f);
            f0le.flexibleWidth = 0f;
        }
        // Frame0 AspectRatioFitter = f0IdealW : finalMatrixH (соотношение какой получилось по весам)
        ApplyFrameFixedSize(frame0, f0IdealW, finalMatrixH, aspectFromSize: true);

        ApplyFrameFixedHeight(frame1, eachRightH, useFlexible: false);
        ApplyFrameFixedHeight(frame2, eachRightH, useFlexible: false);

        ApplyTextPlateGlobalSettingsToAllSlots();

        // ✅ ФОРС-РЕБИЛД всей иерархии LayoutGroup (3 пасса от детей к родителям).
        // Один ForceRebuildLayoutImmediate(contentRoot) НЕ ХВАТАЕТ:
        //   Пасс 1 → пересчитываем preferredWidth/Height для отдельных Frame0/1/2 и RightColumn (LayoutElement)
        //   Пасс 2 → Row HLG пересчитывает ширины детей с учётом matrixFrame0Weight/matrixRightColumnWeight
        //   Пасс 3 → contentRoot VLG выравнивает Row относительно Status/Qte
        // Без тройного ребилда в Unity иногда остаются размеры ПРЕДЫДУЩЕГО кадра (баг: веса 2×3 не применялись)
        var rootCanvasRt = GetComponent<RectTransform>();
        // Пасс 1: дети Row-а (Frame0, RightColumn → Frame1, Frame2)
        if (frame0.root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(frame0.root);
        if (rightCol != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rightCol as RectTransform);
        if (frame1.root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(frame1.root);
        if (frame2.root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(frame2.root);
        // Пасс 2: сам Row
        if (rowAsRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rowAsRect);
        // Пасс 3: contentRoot и корневой Canvas
        if (contentRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        if (rootCanvasRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootCanvasRt);
    }

    private static void ApplyFrameFixedHeight(FrameSlot slot, float fixedH, bool useFlexible = false)
    {
        if (slot == null || slot.root == null) return;
        var fitter = slot.root.GetComponent<AspectRatioFitter>();
        var le = slot.root.GetComponent<LayoutElement>();
        if (le == null) le = slot.root.gameObject.AddComponent<LayoutElement>();
        var rt = slot.root;

        float width = rt.rect.width;
        if (width <= 1f)
        {
            var p = slot.root.parent as RectTransform;
            if (p != null) width = p.rect.width;
            if (width <= 1f) width = fixedH * 16f / 9f;
        }
        if (useFlexible)
        {
            le.flexibleHeight = 1f;
            le.preferredHeight = -1f;
            le.minHeight = -1f;
        }
        else
        {
            le.preferredHeight = fixedH;
            le.minHeight = fixedH;
            le.flexibleHeight = 0f;
        }
        le.preferredWidth = -1f;
        le.flexibleWidth = 1f;
        if (fitter != null)
        {
            fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            fitter.aspectRatio = width / Mathf.Max(1f, fixedH);
        }
    }

    private static void ApplyFrameFixedSize(FrameSlot slot, float fixedW, float fixedH, bool aspectFromSize)
    {
        if (slot == null || slot.root == null) return;
        var fitter = slot.root.GetComponent<AspectRatioFitter>();
        var le = slot.root.GetComponent<LayoutElement>();
        if (le == null) le = slot.root.gameObject.AddComponent<LayoutElement>();

        le.preferredWidth = fixedW;
        le.minWidth = Mathf.Max(100f, fixedW * 0.5f);
        le.flexibleWidth = 0f;
        le.preferredHeight = fixedH;
        le.minHeight = Mathf.Max(60f, fixedH);
        le.flexibleHeight = 0f;

        if (fitter != null)
        {
            fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            if (fixedH > 1f && fixedW > 1f)
                fitter.aspectRatio = fixedW / fixedH;
        }
    }

    /// <summary>Применить глобальные настройки Text Plate ко ВСЕМ 4 слотам.
    /// Нужно чтобы менять высоту/шрифт/паддинги в инспекторе сразу же на всех кадрах и мигрировать старые сцены LevelMenu.</summary>
    private void ApplyTextPlateGlobalSettingsToAllSlots()
    {
        ApplyTextPlateToSlot(frame0);
        ApplyTextPlateToSlot(frame1);
        ApplyTextPlateToSlot(frame2);
    }

    private void ApplyTextPlateToSlot(FrameSlot slot)
    {
        if (slot == null) return;
        // Text Plate height (140 -> textPlateHeight)
        if (slot.textPlateRoot != null)
        {
            Vector2 sd = slot.textPlateRoot.sizeDelta;
            sd.y = textPlateHeight;
            slot.textPlateRoot.sizeDelta = sd;
        }
        // Text Plate alpha color (background)
        if (slot.textPlateBackground != null)
        {
            slot.textPlateBackground.color = new Color(0f, 0f, 0f, textPlateAlpha);
        }
        // TMP text: fontSize + padding offset
        if (slot.text != null)
        {
            slot.text.fontSize = textPlateFontSize;
            // применить паддинги к Text RectTransform (если он есть)
            var textRt = slot.text.rectTransform;
            if (textRt != null)
            {
                textRt.offsetMin = new Vector2(textPlatePadding.x, textPlatePadding.y);
                textRt.offsetMax = new Vector2(-textPlatePadding.x, -textPlatePadding.y);
            }
        }
    }

    private void UpdateContentLayoutForSlots()
    {
        ApplyContentLayoutSizes();
        EnsureMatrixGridAlignment();
        if (contentRoot == null) return;
        LayoutRebuilder.MarkLayoutForRebuild(contentRoot);
    }

    private bool HasValidBuiltLayout()
    {
        string reason = null;
        if (contentRoot == null) { reason = "contentRoot == null"; goto LogFalse; }
        var contentVlg = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (contentVlg == null)
        {
            Debug.LogWarning("[LAYOUT] ⚠️ Content has NO VerticalLayoutGroup! Adding one now with correct flags.", this);
            contentVlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentVlg.childControlHeight = true;
            contentVlg.childControlWidth = true;
            contentVlg.childForceExpandHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.spacing = 0f;
            contentVlg.padding = new RectOffset(0, 0, 0, 0);
            if (contentRoot is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
        if (!contentVlg.childControlHeight || !contentVlg.childControlWidth ||
            !contentVlg.childForceExpandHeight || !contentVlg.childForceExpandWidth)
        {
            Debug.LogWarning("[LAYOUT] ⚠️ Content VLG flags wrong — fixing them now.", this);
            contentVlg.childControlHeight = true;
            contentVlg.childControlWidth = true;
            contentVlg.childForceExpandHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.spacing = 0f;
        }
        // Always keep side margins (Dispatch-style): contentSidePaddingPx left + right
        int pH = Mathf.RoundToInt(contentSidePaddingPx);
        if (contentVlg.padding.left != pH || contentVlg.padding.right != pH)
        {
            contentVlg.padding = new RectOffset(pH, pH, contentVlg.padding.top, contentVlg.padding.bottom);
        }
        // Also make sure Content has a max width LayoutElement so it's contentPreferredWidthPx (default 1800) wide on 1920
        var cle = contentRoot.GetComponent<LayoutElement>();
        if (cle == null) cle = contentRoot.gameObject.AddComponent<LayoutElement>();
        if (Mathf.Abs(cle.preferredWidth - contentPreferredWidthPx) > 1f) cle.preferredWidth = contentPreferredWidthPx;
        if (Mathf.Abs(cle.minWidth - contentMinWidthPx) > 1f) cle.minWidth = contentMinWidthPx;
        if (contentRoot.anchorMin.x != 0.5f || contentRoot.anchorMax.x != 0.5f)
        {
            contentRoot.anchorMin = new Vector2(0.5f, contentRoot.anchorMin.y);
            contentRoot.anchorMax = new Vector2(0.5f, contentRoot.anchorMax.y);
            contentRoot.pivot = new Vector2(0.5f, contentRoot.pivot.y);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(contentPreferredWidthPx, contentRoot.sizeDelta.y);
        }

        int childCount = contentRoot.childCount;
        if (childCount != 3) { reason = $"contentRoot.childCount={childCount}, EXPECTED exactly 3 (Status+Row+Qte)"; goto LogFalse; }

        Transform statusT = contentRoot.GetChild(0);
        Transform rowT = contentRoot.GetChild(1);
        Transform qteT = contentRoot.GetChild(2);
        if (statusT == null || rowT == null || qteT == null) { reason = "one of child[0..2] transforms == null"; goto LogFalse; }
        if (statusT.name != "StatusHeader" || rowT.name != "Row" || qteT.name != "QteBottom")
        { reason = $"children names mismatch: #{statusT?.name} / #{rowT?.name} / #{qteT?.name}, expected StatusHeader/Row/QteBottom"; goto LogFalse; }

        // Auto-hydrate inspector refs (they can be stale after ForceRebuild in Editor).
        if (statusHeaderRoot == null || statusHeaderRoot != statusT)
            statusHeaderRoot = statusT as RectTransform;
        if (qteBottomRoot == null || qteBottomRoot != qteT)
            qteBottomRoot = qteT as RectTransform;
        if (statusHeaderRoot == null || qteBottomRoot == null) { reason = "statusHeaderRoot/qteBottomRoot failed to hydrate RectTransform"; goto LogFalse; }

        // Ensure layout element components exist (never fail — just add if missing).
        if (statusHeaderRoot.GetComponent<LayoutElement>() == null)
            statusHeaderRoot.gameObject.AddComponent<LayoutElement>();
        if (qteBottomRoot.GetComponent<LayoutElement>() == null)
            qteBottomRoot.gameObject.AddComponent<LayoutElement>();

        // Row structure: HLG + flexibleHeight 1.
        var rowLe = rowT.GetComponent<LayoutElement>();
        if (rowLe == null)
        {
            rowLe = rowT.gameObject.AddComponent<LayoutElement>();
            rowLe.flexibleHeight = 1f;
        }
        if (Mathf.Abs(rowLe.flexibleHeight - 1f) > 0.001f) rowLe.flexibleHeight = 1f;
        if (rowT.GetComponent<HorizontalLayoutGroup>() == null) { reason = "Row has NO HorizontalLayoutGroup"; goto LogFalse; }

        var left = rowT.Find("Frame0_BigLeft");
        var right = rowT.Find("RightColumn");
        var top = right != null ? right.Find("Frame1_TopRight") : null;
        var bottom = right != null ? right.Find("Frame2_BottomRight") : null;
        if (left == null || right == null || top == null || bottom == null)
        { reason = $"Row sub-nodes missing: Frame0_BigLeft={left != null}, RightColumn={right != null}, Frame1_TopRight={top != null}, Frame2_BottomRight={bottom != null}"; goto LogFalse; }
        if (right.GetComponent<VerticalLayoutGroup>() == null) { reason = "RightColumn has NO VerticalLayoutGroup"; goto LogFalse; }

        // Soft hydration only — don't fail if refs are missing (page hasn't been loaded yet).
        if (statusHeaderBackground == null)
            statusHeaderBackground = statusHeaderRoot.GetComponent<Image>();
        if (qteBottomBackground == null)
            qteBottomBackground = qteBottomRoot.GetComponent<Image>();
        if (statusHeaderGroup == null)
            statusHeaderGroup = statusHeaderRoot.GetComponent<CanvasGroup>();
        if (qteBottomGroup == null)
            qteBottomGroup = qteBottomRoot.GetComponent<CanvasGroup>();

        if ((frame0 == null || frame0.root == null) && left != null) frame0 = CaptureExistingSlot(left);
        if ((frame1 == null || frame1.root == null) && top != null) frame1 = CaptureExistingSlot(top);
        if ((frame2 == null || frame2.root == null) && bottom != null) frame2 = CaptureExistingSlot(bottom);

        Debug.Log("[LAYOUT] ✅ HasValid = TRUE (structure matches).", this);
        ApplyTextPlateGlobalSettingsToAllSlots(); // Мигрируем уже существующие Text Plate в LevelMenu под новые глобальные настройки
        return true;

LogFalse:
        Debug.LogWarning($"[LAYOUT] ❌ HasValid = FALSE — reason: {reason}", this);
        return false;
    }

    private void CleanupDuplicateGeneratedLayout()
    {
        if (contentRoot == null) return;

        if (statusHeaderRoot != null && statusHeaderRoot.parent != contentRoot)
            statusHeaderRoot = null;
        if (qteBottomRoot != null && qteBottomRoot.parent != contentRoot)
            qteBottomRoot = null;

        // Never auto-destroy generated content at runtime — too risky (can wipe scene we saved in
        // Edit Mode). Only the explicit ForceRebuildLayout context menu is allowed to destroy.
        // If the structure is wrong, BuildIfMissing will add missing pieces.
        frame0 = null;
        frame1 = null;
        frame2 = null;
    }

    private void NormalizeContentRootChildrenFixed()
    {
        if (contentRoot == null) return;

        System.Collections.Generic.List<Transform> allStatus = new System.Collections.Generic.List<Transform>();
        System.Collections.Generic.List<Transform> allRows = new System.Collections.Generic.List<Transform>();
        System.Collections.Generic.List<Transform> allQte = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < contentRoot.childCount; i++)
        {
            var c = contentRoot.GetChild(i);
            if (c == null) continue;
            if (c.name == "StatusHeader") allStatus.Add(c);
            else if (c.name == "Row") allRows.Add(c);
            else if (c.name == "QteBottom") allQte.Add(c);
        }

        bool hasDuplicates = allStatus.Count > 1 || allRows.Count > 1 || allQte.Count > 1;
        bool hasAll = allStatus.Count >= 1 && allRows.Count >= 1 && allQte.Count >= 1;

        Debug.Log($"[LAYOUT] Normalize: before — Status={allStatus.Count}, Row={allRows.Count}, Qte={allQte.Count} | hasDuplicates={hasDuplicates}, hasAll={hasAll}", this);

        // Pick best Row: the one with Frame0_BigLeft inside (real layout, not empty leftover)
        Transform bestStatus = allStatus.Count > 0 ? allStatus[0] : null;
        Transform bestRow = null;
        Transform bestQte = allQte.Count > 0 ? allQte[0] : null;
        for (int i = 0; i < allRows.Count; i++)
        {
            var r = allRows[i];
            bool looksValid = r.Find("Frame0_BigLeft") != null && r.Find("RightColumn") != null;
            if (looksValid) { bestRow = r; Debug.Log($"[LAYOUT] Normalize: pick Row #{i} as bestRow (has Frame0_BigLeft + RightColumn)", this); break; }
        }
        if (bestRow == null && allRows.Count > 0) { bestRow = allRows[0]; Debug.Log($"[LAYOUT] Normalize: no Row has Frame structure → fallback to Row #0 first found", this); }

        // If duplicates -> destroy extras
        if (hasDuplicates)
        {
            System.Collections.Generic.List<Transform> toKill = new System.Collections.Generic.List<Transform>();
            foreach (var s in allStatus) if (s != bestStatus) toKill.Add(s);
            foreach (var r in allRows) if (r != bestRow) toKill.Add(r);
            foreach (var q in allQte) if (q != bestQte) toKill.Add(q);
            foreach (var t in toKill)
            {
                if (t == null) continue;
                Debug.Log($"[LAYOUT] Normalize: DESTROY duplicate extra '{t.name}' (Sibling {t.GetSiblingIndex()})", this);
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject, false);
            }
        }
        else if (!hasAll)
        {
            // Missing some — delete all status/row/qte ones, Build will recreate cleanly
            System.Collections.Generic.List<Transform> toKill = new System.Collections.Generic.List<Transform>();
            toKill.AddRange(allStatus);
            toKill.AddRange(allRows);
            toKill.AddRange(allQte);
            foreach (var t in toKill)
            {
                if (t == null) continue;
                Debug.Log($"[LAYOUT] Normalize: hasAll=false → DESTROY existing '{t.name}' to recreate all 3 cleanly", this);
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject, false);
            }
            bestStatus = null;
            bestRow = null;
            bestQte = null;
        }

        // Ensure correct sibling order: Status (0) -> Row (1) -> Qte (2)
        if (bestStatus != null && bestStatus.parent == contentRoot) bestStatus.SetSiblingIndex(0);
        if (bestRow != null && bestRow.parent == contentRoot) bestRow.SetSiblingIndex(1);
        if (bestQte != null && bestQte.parent == contentRoot) bestQte.SetSiblingIndex(2);

        if (bestStatus != null) statusHeaderRoot = bestStatus as RectTransform;
        if (bestRow != null)
        {
            // refresh slot proxies (soft)
            if (frame0 == null || frame0.root == null)
            {
                var left = bestRow.Find("Frame0_BigLeft");
                var right = bestRow.Find("RightColumn");
                var top = right != null ? right.Find("Frame1_TopRight") : null;
                var bottom = right != null ? right.Find("Frame2_BottomRight") : null;
                if (left != null) frame0 = CaptureExistingSlot(left);
                if (top != null) frame1 = CaptureExistingSlot(top);
                if (bottom != null) frame2 = CaptureExistingSlot(bottom);
            }
        }
        if (bestQte != null) qteBottomRoot = bestQte as RectTransform;
        Debug.Log($"[LAYOUT] Normalize: done — bestStatus={(bestStatus ? bestStatus.name : "NULL")}, bestRow={(bestRow ? bestRow.name : "NULL")}, bestQte={(bestQte ? bestQte.name : "NULL")}", this);
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
            slot.previewProxy = sprite.GetComponent<ComicPreviewFrameProxy>();
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

        // ВСЕ Frame-КОНТЕЙНЕРЫ всегда 16:9 — чётко фиксированное соотношение. Без флагов.
        var rootGo = new GameObject(name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(RectMask2D),
            typeof(LayoutElement),
            typeof(AspectRatioFitter));
        rootGo.transform.SetParent(parent, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var rootImage = rootGo.GetComponent<Image>();
        rootImage.color = Color.black;
        rootImage.raycastTarget = false;
        rootImage.preserveAspect = false;

        var rootLe = rootGo.GetComponent<LayoutElement>();
        rootLe.minWidth = 100f;
        rootLe.minHeight = 56f;           // 100 * 9/16
        rootLe.flexibleWidth = 1f;        // шириной управляет parent (HLG/VLG)
        rootLe.flexibleHeight = 0f;       // высотой НЕ управляет parent — только наш AspectRatioFitter!

        var rootFitter = rootGo.GetComponent<AspectRatioFitter>();
        rootFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        rootFitter.aspectRatio = 16f / 9f;

        var spriteGo = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter), typeof(ComicPreviewFrameProxy));
        spriteGo.transform.SetParent(rootGo.transform, false);
        var spriteRt = spriteGo.GetComponent<RectTransform>();
        spriteRt.anchorMin = Vector2.zero;
        spriteRt.anchorMax = Vector2.one;
        spriteRt.pivot = new Vector2(0.5f, 0.5f);
        spriteRt.offsetMin = Vector2.zero;
        spriteRt.offsetMax = Vector2.zero;
        var spriteImage = spriteGo.GetComponent<Image>();
        spriteImage.color = Color.white;
        spriteImage.raycastTarget = false;
        spriteImage.preserveAspect = false;     // вписывает fitter
        spriteImage.type = Image.Type.Simple;
        // ВАЖНО: спрайт всегда сохраняет пропорции и вписывается в Frame-слот целиком.
        // Если aspect не совпадает (например кадр 16:9 а слот 2.3:1) — появятся чёрные полосы letterbox.
        // Если нужно БЕЗ полос (обрезать края) → поменяй на AspectMode.EnvelopeParent
        var spriteFitter = spriteGo.GetComponent<AspectRatioFitter>();
        spriteFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        spriteFitter.aspectRatio = 16f / 9f;
        var previewProxy = spriteGo.GetComponent<ComicPreviewFrameProxy>();

        var group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        var plateGo = new GameObject("TextPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        plateGo.transform.SetParent(rootGo.transform, false);
        var plateRt = plateGo.GetComponent<RectTransform>();
        plateRt.anchorMin = new Vector2(0f, 0f);
        plateRt.anchorMax = new Vector2(1f, 0f);
        plateRt.pivot = new Vector2(0.5f, 0f);
        plateRt.sizeDelta = new Vector2(0f, textPlateHeight);
        plateRt.anchoredPosition = new Vector2(0f, 0f);

        var plateBg = plateGo.GetComponent<Image>();
        plateBg.color = new Color(0f, 0f, 0f, textPlateAlpha);
        plateBg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(plateGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(textPlatePadding.x, textPlatePadding.y);
        textRt.offsetMax = new Vector2(-textPlatePadding.x, -textPlatePadding.y);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = string.Empty;
        tmp.fontSize = textPlateFontSize;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        var textGroup = plateGo.GetComponent<CanvasGroup>();
        textGroup.alpha = 0f;

        slot.root = rootRt;
        slot.group = group;
        slot.image = spriteImage;
        slot.spriteRoot = spriteRt;
        slot.previewProxy = previewProxy;
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
        if (slot.root == null) return;
        var rootImage = slot.root.GetComponent<Image>();
        if (rootImage != null && rootImage.color != Color.black) rootImage.color = Color.black;
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

    private void HideStatusHeader()
    {
        if (statusHeaderGroup != null) statusHeaderGroup.alpha = 0f;
        if (statusHeaderGroup != null)
        {
            statusHeaderGroup.interactable = false;
            statusHeaderGroup.blocksRaycasts = false;
        }
        if (statusHeaderBackground != null)
        {
            var orig = statusHeaderBackground.color;
            orig.a = 0f;
            statusHeaderBackground.color = orig;
        }
        if (statusHeaderBannerText != null) statusHeaderBannerText.gameObject.SetActive(false);
        ClearGeneratedStatusPills();
    }

    private void ShowStatusHeader()
    {
        if (statusHeaderRoot != null && !statusHeaderRoot.gameObject.activeSelf) statusHeaderRoot.gameObject.SetActive(true);
        if (statusHeaderGroup != null) statusHeaderGroup.alpha = 1f;
        if (statusHeaderGroup != null)
        {
            statusHeaderGroup.interactable = false;
            statusHeaderGroup.blocksRaycasts = false;
        }
        if (statusHeaderBackground != null)
        {
            var orig = statusHeaderBackground.color;
            orig.a = 0.72f;
            statusHeaderBackground.color = orig;
        }
    }

    private void HideQteBottom()
    {
        if (qteBottomGroup != null) qteBottomGroup.alpha = 0f;
        if (qteBottomGroup != null)
        {
            qteBottomGroup.interactable = false;
            qteBottomGroup.blocksRaycasts = false;
        }
        if (qteBottomBackground != null)
        {
            var orig = qteBottomBackground.color;
            orig.a = 0f;
            qteBottomBackground.color = orig;
        }
        if (qteTimerFill != null) qteTimerFill.fillAmount = 1f;
        if (qteTimerText != null) qteTimerText.text = string.Empty;
        ClearGeneratedQteOptions();
        _waitingForQteChoice = false;
    }

    private void ShowQteBottom()
    {
        if (qteBottomRoot != null && !qteBottomRoot.gameObject.activeSelf) qteBottomRoot.gameObject.SetActive(true);
        if (qteBottomGroup != null) qteBottomGroup.alpha = 1f;
        if (qteBottomGroup != null)
        {
            qteBottomGroup.interactable = true;
            qteBottomGroup.blocksRaycasts = true;
        }
        if (qteBottomBackground != null)
        {
            var orig = qteBottomBackground.color;
            orig.a = 0.78f;
            qteBottomBackground.color = orig;
        }
    }

    private void ClearGeneratedStatusPills()
    {
        for (int i = _generatedPills.Count - 1; i >= 0; i--)
        {
            var go = _generatedPills[i];
            if (go != null) DestroyGeneratedObject(go);
        }
        _generatedPills.Clear();
    }

    private void ClearGeneratedQteOptions()
    {
        for (int i = _generatedOptions.Count - 1; i >= 0; i--)
        {
            var go = _generatedOptions[i];
            if (go != null) DestroyGeneratedObject(go);
        }
        _generatedOptions.Clear();
    }

    private void ApplyStatusHeaderForPage(ComicPage page, bool previewMode)
    {
        HideStatusHeader();
        ClearGeneratedStatusPills();

        var config = page.statusBar;
        bool show = config.showStatusHeader;
        if (!show && (config.statusPills == null || config.statusPills.Count == 0) && string.IsNullOrWhiteSpace(config.topBannerText))
        {
            return;
        }

        if (statusHeaderRoot == null) return;
        ShowStatusHeader();

        if (statusHeaderBannerText != null)
        {
            statusHeaderBannerText.text = string.IsNullOrWhiteSpace(config.topBannerText) ? string.Empty : config.topBannerText;
            statusHeaderBannerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusHeaderBannerText.text));
        }

        if (statusHeaderPillsRow == null) return;
        if (config.statusPills == null || config.statusPills.Count == 0) return;

        for (int i = 0; i < config.statusPills.Count; i++)
        {
            var pill = config.statusPills[i];
            var pillGo = new GameObject("Pill_" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
            pillGo.transform.SetParent(statusHeaderPillsRow.transform, false);
            _generatedPills.Add(pillGo);

            var pillImg = pillGo.GetComponent<Image>();
            Color tint = pill.tintColor == default(Color) ? new Color(1f, 1f, 1f, 0.12f) : pill.tintColor;
            if (tint.a <= 0.01f) tint.a = 0.12f;
            pillImg.color = tint;
            pillImg.raycastTarget = false;

            var hlg = pillGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(14, 16, 8, 8);

            bool hasIcon = pill.icon != null;
            if (hasIcon)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(pillGo.transform, false);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = pill.icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.sizeDelta = new Vector2(22f, 22f);
                var iconLe = iconGo.AddComponent<LayoutElement>();
                iconLe.minWidth = 22f;
                iconLe.preferredWidth = 22f;
                iconLe.minHeight = 22f;
                iconLe.preferredHeight = 22f;
                iconLe.flexibleWidth = 0f;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(pillGo.transform, false);
            var labelTmp = labelGo.GetComponent<TMP_Text>();
            string label = string.IsNullOrWhiteSpace(pill.label) ? "STAT" : pill.label;
            string value = string.IsNullOrWhiteSpace(pill.valueText) ? string.Empty : pill.valueText;
            labelTmp.text = string.IsNullOrWhiteSpace(value) ? label : (label + ": " + value);
            labelTmp.fontSize = 22f;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.enableWordWrapping = false;
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.minWidth = 0f;
            labelLe.flexibleWidth = 0f;
        }
    }

    private void SetStatusBannerTemporarily(string text, float seconds)
    {
        if (statusHeaderBannerText == null) return;
        if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
        _bannerRoutine = StartCoroutine(TempBannerRoutine(text, seconds));
    }

    private static string BuildConsequenceBannerText(string explicitBanner,
        int ctBefore, int ppBefore, int nsBefore, int nwBefore, int mnBefore,
        int ctAfter, int ppAfter, int nsAfter, int nwAfter, int mnAfter)
    {
        if (!string.IsNullOrWhiteSpace(explicitBanner)) return explicitBanner;

        int dCT = ctAfter - ctBefore;
        int dPP = ppAfter - ppBefore;
        int dNS = nsAfter - nsBefore;
        int dNW = nwAfter - nwBefore;
        int dMN = mnAfter - mnBefore;

        var parts = new List<string>();
        if (dCT > 0) parts.Add("Шарлотта это запомнит.");
        else if (dCT < 0) parts.Add("Шарлотта расстроена.");
        if (dNW > 0) parts.Add("Ник теплее к тебе.");
        else if (dNW < 0) parts.Add("Ник холоден.");
        if (dNS > 0) parts.Add("Стресс Ника растёт.");
        if (dPP > 0) parts.Add("Патрик давит сильнее.");
        if (dMN != 0) parts.Add(dMN > 0 ? "+" + dMN + "$" : (dMN + "$"));

        return parts.Count == 0 ? null : string.Join("  ", parts);
    }

    private IEnumerator TempBannerRoutine(string text, float seconds)
    {
        if (statusHeaderBannerText == null) yield break;
        bool wasAlreadyShown = statusHeaderGroup != null && statusHeaderGroup.alpha > 0.01f;
        if (!wasAlreadyShown) ShowStatusHeader();

        string original = statusHeaderBannerText.text;
        statusHeaderBannerText.text = string.IsNullOrEmpty(text) ? original : text;
        statusHeaderBannerText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));

        statusHeaderBannerText.text = original ?? string.Empty;
        bool shouldKeep = !string.IsNullOrWhiteSpace(statusHeaderBannerText.text) || wasAlreadyShown;
        statusHeaderBannerText.gameObject.SetActive(shouldKeep || !string.IsNullOrWhiteSpace(statusHeaderBannerText.text));
        if (!shouldKeep) HideStatusHeader();
        _bannerRoutine = null;
    }

    private void ApplyQteBottomForPage(ComicPage page, bool previewMode)
    {
        HideQteBottom();
        ClearGeneratedQteOptions();

        if (!page.HasQte) return;
        if (qteBottomRoot == null) return;

        if (previewMode) ShowQteBottom();

        var qte = page.qte;
        if (qteQuestionText != null)
        {
            bool hasQuestion = !string.IsNullOrWhiteSpace(qte.questionText);
            qteQuestionText.gameObject.SetActive(hasQuestion);
            qteQuestionText.text = hasQuestion ? qte.questionText : string.Empty;
        }

        if (qteTimerFill != null) qteTimerFill.fillAmount = 1f;
        if (qteTimerText != null)
        {
            qteTimerText.text = string.Empty;
            qteTimerText.gameObject.SetActive(false);
        }

        if (qteOptionsGrid == null) return;

        int optionCount = qte.options == null ? 0 : qte.options.Count;
        int columns = optionCount <= 3 ? (optionCount <= 0 ? 1 : optionCount) : 2;
        qteOptionsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        qteOptionsGrid.constraintCount = columns;

        float singleCellWidth = columns <= 3 ? 420f : 640f;
        bool hasAnySubtitleOrLockReason = false;
        StoryRuntimeState optionStateCheck = StoryRuntimeState.Instance;
        if (qte.options != null)
        {
            for (int i = 0; i < qte.options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(qte.options[i].subtitle))
                { hasAnySubtitleOrLockReason = true; break; }
                string rDump;
                var lk = qte.options[i].EvaluateLock(optionStateCheck, out rDump);
                if ((lk == ComicQteLockState.SoftLocked || lk == ComicQteLockState.HardLocked) && !string.IsNullOrWhiteSpace(rDump))
                { hasAnySubtitleOrLockReason = true; break; }
            }
        }
        float cellHeight = hasAnySubtitleOrLockReason ? 104f : 72f;
        qteOptionsGrid.cellSize = new Vector2(singleCellWidth, cellHeight);

        StoryRuntimeState optionState = StoryRuntimeState.Instance;
        for (int i = 0; i < optionCount; i++)
        {
            var option = qte.options[i];
            string optionReason;
            var effectiveLock = option.EvaluateLock(optionState, out optionReason);

            var optionGo = new GameObject("QteOption_" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(LayoutElement));
            optionGo.transform.SetParent(qteOptionsGrid.transform, false);
            _generatedOptions.Add(optionGo);

            var bg = optionGo.GetComponent<Image>();
            bool hardLock = effectiveLock == ComicQteLockState.HardLocked;
            bool softLock = effectiveLock == ComicQteLockState.SoftLocked;
            Color baseColor = hardLock ? new Color(0.3f, 0.15f, 0.15f, 0.95f) :
                              softLock ? new Color(0.22f, 0.22f, 0.28f, 0.95f) :
                                         new Color(0.12f, 0.14f, 0.22f, 0.95f);
            bg.color = baseColor;
            bg.raycastTarget = true;

            var le = optionGo.GetComponent<LayoutElement>();
            le.preferredWidth = singleCellWidth;
            le.preferredHeight = cellHeight;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
            le.minWidth = 0f;
            le.minHeight = 0f;

            var button = optionGo.GetComponent<Button>();
            var cg = optionGo.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = !hardLock;
            cg.blocksRaycasts = !hardLock;
            button.interactable = !hardLock;

            var optionRoot = optionGo.GetComponent<RectTransform>();
            optionRoot.anchorMin = new Vector2(0.5f, 0.5f);
            optionRoot.anchorMax = new Vector2(0.5f, 0.5f);
            optionRoot.pivot = new Vector2(0.5f, 0.5f);

            var vlg = optionGo.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.padding = new RectOffset(24, 24, 12, 12);
            vlg.spacing = 4f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(optionGo.transform, false);
            var labelTmp = labelGo.GetComponent<TMP_Text>();
            string displayLabel = string.IsNullOrWhiteSpace(option.label) ? ("Вариант " + (i + 1)) : option.label;
            string displaySubtitle = string.IsNullOrWhiteSpace(option.subtitle) ? null : option.subtitle;
            if (displaySubtitle == null)
            {
                int slashIdx = displayLabel.IndexOf('/');
                if (slashIdx > 0 && slashIdx < displayLabel.Length - 1)
                {
                    string a = displayLabel.Substring(0, slashIdx).Trim();
                    string b = displayLabel.Substring(slashIdx + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
                    {
                        displayLabel = a;
                        displaySubtitle = b;
                    }
                }
            }
            labelTmp.text = displayLabel;
            labelTmp.fontSize = 26f;
            labelTmp.alignment = TextAlignmentOptions.Midline;
            labelTmp.color = hardLock ? new Color(1f, 0.7f, 0.7f, 1f) : softLock ? new Color(0.85f, 0.85f, 0.9f, 1f) : Color.white;
            labelTmp.enableWordWrapping = true;

            var subtitleGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subtitleGo.transform.SetParent(optionGo.transform, false);
            var subTmp = subtitleGo.GetComponent<TMP_Text>();
            subTmp.text = string.IsNullOrWhiteSpace(displaySubtitle) ? string.Empty : displaySubtitle;
            subTmp.fontSize = 22f;
            subTmp.alignment = TextAlignmentOptions.Midline;
            subTmp.color = hardLock ? new Color(1f, 0.75f, 0.75f, 0.9f) : softLock ? new Color(0.8f, 0.8f, 0.85f, 0.9f) : new Color(0.88f, 0.88f, 0.92f, 0.95f);
            subTmp.enableWordWrapping = true;
            subtitleGo.SetActive(!string.IsNullOrWhiteSpace(displaySubtitle));

            var reasonGo = new GameObject("Reason", typeof(RectTransform), typeof(TextMeshProUGUI));
            reasonGo.transform.SetParent(optionGo.transform, false);
            var reasonTmp = reasonGo.GetComponent<TMP_Text>();
            bool showReason = (softLock || hardLock) && !string.IsNullOrWhiteSpace(optionReason);
            reasonTmp.text = showReason ? optionReason : string.Empty;
            reasonTmp.fontSize = 18f;
            reasonTmp.alignment = TextAlignmentOptions.Midline;
            reasonTmp.color = hardLock ? new Color(1f, 0.6f, 0.6f, 1f) : new Color(0.75f, 0.75f, 0.85f, 1f);
            reasonTmp.enableWordWrapping = true;
            reasonGo.SetActive(showReason);

            ComicQteLockState capturedLock = effectiveLock;
            ComicQteOption capturedOption = option;
            ComicPage capturedPage = page;
            button.onClick.AddListener(() =>
            {
                if (capturedLock == ComicQteLockState.HardLocked) return;
                if (_isPreviewMode) return;
                HandleQteChoice(capturedPage, capturedOption);
            });
        }
    }
}
