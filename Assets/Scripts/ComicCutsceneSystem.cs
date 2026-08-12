using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ComicCutsceneSystem : MonoBehaviour
{
    [Header("Config")]
    public float fadeDuration = 0.4f;
    public bool playBeforeFirstLevel = true;
    public int firstGameplayLevelBuildIndex = 1;
    public bool dontDestroyOnLoad = true;

    [Header("Visual")]
    [Min(0)] public int frameBorderPixels = 4;
    [Range(0f, 1f)] public float textPlateAlpha = 0.85f;
    public TMP_FontAsset frameTextFontOverride;
    [Min(0f)] public float frameTextFontSizeOverride = 0f;

    [Header("QTE / Status Layout (override)")]
    [Min(0f)] public float statusHeaderHeightPx = 110f;
    [Min(0f)] public float statusHeaderTopPaddingPx = 24f;
    [Min(0f)] public float statusHeaderHorizontalPaddingPx = 40f;
    [Min(0f)] public float qteBottomHeightPx = 300f;
    [Min(0f)] public float qteBottomBottomPaddingPx = 28f;
    [Min(0f)] public float qteBottomHorizontalPaddingPx = 40f;

    [Header("Matrix Size (Critical for Layout)")]
    [Range(0.5f, 1.8f)] public float matrixZoom = 1.1f;
    [Tooltip("0.1…1.0. Какая ДОЛЯ доступной вертикали отводится под матрицу кадров. 0.95 = 95% (дефолт). 1.0 = под завязку. Позволяет отдельно управлять ВЫСОТОЙ матрицы НЕЗАВИСИМО от того насколько широкий Frame0 или правая колонка.")]
    [Range(0.1f, 1.0f)] public float matrixHeightPercentOfAvailable = 0.95f;
    [Tooltip("Вес ширины Frame0 (большой кадр слева) относительно RightColumn. Дефолт 3 = 60% (при Right=2). Поставь 2 или 1.5 чтобы Frame0 стал уже, а правая колонка шире.")]
    [Min(0.1f)] public float matrixFrame0Weight = 3f;
    [Tooltip("Вес ширины RightColumn (Frame1+Frame2) относительно Frame0. Дефолт 2 = 40% (при Frame0=3). Поставь 2.5 или 3 чтобы два маленьких справа стали шире.")]
    [Min(0.1f)] public float matrixRightColumnWeight = 2f;
    [Min(0f)] public float matrixHorizontalSpacingPx = 14f;
    [Min(0f)] public float matrixVerticalSpacingPx = 10f;

    [Header("Story State (for QTE effects)")]
    public StoryRuntimeState runtimeState;
    public Act1Archetype defaultArchetype = Act1Archetype.Overwhelmed;

    [Header("Sequences")]
    public List<ComicSequence> sequences = new List<ComicSequence>();

    [Header("UI")]
    public ComicCutsceneUI ui;

    [Header("Editor Preview")]
    public ComicSequence previewSequence;
    [Min(0)] public int previewPageIndex = 0;
    [Range(1, 3)] public int previewVisibleFrames = 3;

    private bool _playing;
    private static readonly HashSet<int> _playedTriggersThisSession = new HashSet<int>();
    private static ComicCutsceneSystem _instance;
#if UNITY_EDITOR
    [System.NonSerialized] private ComicCutsceneUI _editorPreviewUi;
#endif

    // ✅ Unity вызывает OnValidate() АВТОМАТИЧЕСКИ каждый раз когда ты меняешь ЛЮБОЕ поле в System инспекторе!
    // Пробрасываем matrix веса / проценты сразу в UI — так что меняешь в ComicCutsceneSystem → сразу видишь результат!
    private void OnValidate()
    {
        if (this == null) return;
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (Application.isPlaying) return;
            try
            {
                // Сценарий А: ты меняешь цифры в ComicCutsceneSystem когда на сцене УЖЕ открыт UI (в PreviewMode)
                if (ui == null) ui = FindObjectOfType<ComicCutsceneUI>(true);
                if (ui != null && ui.gameObject.activeInHierarchy)
                {
                    ApplyLayoutSizesToUi(ui);
                    ApplyVisualSettingsToUi();
                }
                // Сценарий Б: Editor Preview Window (_editorPreviewUi)
#if UNITY_EDITOR
                if (_editorPreviewUi != null)
                {
                    ApplyLayoutSizesToUi(_editorPreviewUi);
                    ApplyVisualSettingsToUi();
                }
#endif
            }
            catch (System.Exception) { /* В редакторе игнорируем невалидные состояния */ }
        };
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (Application.isPlaying)
        {
            DestroyDuplicateSystemsInPlayMode();
        }

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureRuntimeStateInitialized();

        if (ui == null) ui = FindObjectOfType<ComicCutsceneUI>(true);
        if (ui != null)
        {
            ApplyVisualSettingsToUi();
            ui.Initialize(fadeDuration);
            ui.RefreshBorders();
        }
    }

    private void EnsureRuntimeStateInitialized()
    {
        if (runtimeState == null) runtimeState = StoryRuntimeState.Instance;
        StoryRuntimeState.ReplaceInstance(runtimeState);
        if (Application.isPlaying)
        {
            runtimeState.archetype = defaultArchetype;
            runtimeState.ResetFromCurrentArchetype();
        }
    }

    private void DestroyDuplicateSystemsInPlayMode()
    {
        var all = FindObjectsByType<ComicCutsceneSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var other = all[i];
            if (other == null) continue;
            if (other == this) continue;
            Destroy(other.gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        TryAutoPlayBeforeFirstLevel();
        SaveStoryLevelCheckpointForCurrentScene();
    }

    public bool TryPlayAfterLevel(int completedLevelBuildIndex, int nextSceneBuildIndex)
    {
        if (!GameFlowState.ShouldPlayComics) return false;
        if (_playing) return true;

        var matches = CollectSequencesForTrigger(completedLevelBuildIndex);
        if (matches.Count == 0) return false;

        if (GameFlowState.CurrentMode == GameFlowMode.Story)
        {
            StorySaveManager.SaveComicResume(completedLevelBuildIndex, nextSceneBuildIndex);
        }

        PlaySequences(matches, nextSceneBuildIndex, loadSceneOnFinish: true);
        return true;
    }

    public bool TryPlayTrigger(int triggerAfterLevelIndex, int nextSceneBuildIndex, bool loadSceneOnFinish)
    {
        if (!GameFlowState.ShouldPlayComics) return false;
        if (_playing) return true;

        var matches = CollectSequencesForTrigger(triggerAfterLevelIndex);
        if (matches.Count == 0) return false;

        if (triggerAfterLevelIndex == 0)
        {
            _playedTriggersThisSession.Add(0);
        }

        if (GameFlowState.CurrentMode == GameFlowMode.Story)
        {
            StorySaveManager.SaveComicResume(triggerAfterLevelIndex, nextSceneBuildIndex);
        }

        PlaySequences(matches, nextSceneBuildIndex, loadSceneOnFinish);
        return true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoPlayBeforeFirstLevel();
        SaveStoryLevelCheckpointForCurrentScene();
    }

    private void TryAutoPlayBeforeFirstLevel()
    {
        if (!GameFlowState.ShouldPlayComics) return;
        if (_playing) return;
        if (!playBeforeFirstLevel) return;
        if (SceneManager.GetActiveScene().buildIndex != firstGameplayLevelBuildIndex) return;
        if (_playedTriggersThisSession.Contains(0)) return;

        var save = StorySaveManager.LoadOrCreate();
        if (save.resumeType == StoryResumeType.Level && save.levelBuildIndex == firstGameplayLevelBuildIndex)
        {
            _playedTriggersThisSession.Add(0);
            return;
        }

        var matches = CollectSequencesForTrigger(0);
        if (matches.Count == 0) return;

        _playedTriggersThisSession.Add(0);
        PlaySequences(matches, nextSceneBuildIndex: firstGameplayLevelBuildIndex, loadSceneOnFinish: false);
    }

    private void SaveStoryLevelCheckpointForCurrentScene()
    {
        if (GameFlowState.CurrentMode != GameFlowMode.Story) return;

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentSceneIndex < firstGameplayLevelBuildIndex) return;
        if (_playing) return;

        StorySaveManager.SaveLevelResume(currentSceneIndex);
    }

    private List<ComicSequence> CollectSequencesForTrigger(int triggerAfterLevelIndex)
    {
        var result = new List<ComicSequence>();
        StoryRuntimeState state = runtimeState != null ? runtimeState : StoryRuntimeState.Instance;
        for (int i = 0; i < sequences.Count; i++)
        {
            var seq = sequences[i];
            if (seq == null) continue;
            if (seq.triggerAfterLevelIndex != triggerAfterLevelIndex) continue;
            if (seq.pages == null || seq.pages.Count == 0) continue;
            if (!seq.MatchesConditions(state)) continue;
            result.Add(seq);
        }
        return result;
    }

    private void PlaySequences(List<ComicSequence> list, int nextSceneBuildIndex, bool loadSceneOnFinish)
    {
        if (list == null || list.Count == 0) return;

        if (ui == null)
        {
            var go = new GameObject("ComicCutsceneUI");
            ui = go.AddComponent<ComicCutsceneUI>();
            ui.buildIfMissingInPlayMode = true;
            ui.frameBorderPixels = frameBorderPixels;
            ui.textPlateAlpha = textPlateAlpha;
            ui.frameTextFontOverride = frameTextFontOverride;
            ui.frameTextFontSizeOverride = frameTextFontSizeOverride;
            ApplyLayoutSizesToUi(ui);
            ui.Initialize(fadeDuration);
        }
        ApplyVisualSettingsToUi();
        if (!ui.gameObject.activeSelf) ui.gameObject.SetActive(true);
        ui.RefreshBorders();

        if (dontDestroyOnLoad && loadSceneOnFinish && ui != null)
        {
            if (transform.parent != null) transform.SetParent(null, worldPositionStays: false);
            if (ui.transform.parent != transform) ui.transform.SetParent(transform, worldPositionStays: false);
            DontDestroyOnLoad(gameObject);
        }

        _playing = true;
        StartCoroutine(TrackUntilUiFinishes());
        ui.Play(list.ToArray(), nextSceneBuildIndex, loadSceneOnFinish);
    }

    private IEnumerator TrackUntilUiFinishes()
    {
        while (ui != null && ui.IsRuntimeShowing)
        {
            yield return null;
        }
        _playing = false;
        SaveStoryLevelCheckpointForCurrentScene();
    }

    public void EditorPreviewSelected()
    {
        if (previewSequence == null || previewSequence.pages == null || previewSequence.pages.Count == 0) return;

        var previewUi = EnsurePreviewUiExists();
        // ✅ СНАЧАЛА применяем ВСЕ настройки layout (matrixFrame0Weight, matrixRightColumnWeight, percent!)
        ApplyLayoutSizesToUi(previewUi);
        ApplyVisualSettingsToUi(previewUi);
        previewPageIndex = Mathf.Clamp(previewPageIndex, 0, previewSequence.pages.Count - 1);
        previewVisibleFrames = Mathf.Clamp(previewVisibleFrames, 1, 3);
        previewUi.SetPreviewBinding(previewSequence, previewPageIndex);
        previewUi.ShowPreviewPage(previewSequence.pages[previewPageIndex], previewVisibleFrames);
    }

    public void EditorClearPreview()
    {
        var previewUi = GetEditorPreviewUi();
        if (previewUi == null) return;
        previewUi.ClearPreviewBinding();
        previewUi.ClearPreview();
    }

    public void EditorPreviewNextPage()
    {
        if (previewSequence == null || previewSequence.pages == null || previewSequence.pages.Count == 0) return;
        previewPageIndex = Mathf.Min(previewPageIndex + 1, previewSequence.pages.Count - 1);
        EditorPreviewSelected();
    }

    public void EditorPreviewPreviousPage()
    {
        if (previewSequence == null || previewSequence.pages == null || previewSequence.pages.Count == 0) return;
        previewPageIndex = Mathf.Max(previewPageIndex - 1, 0);
        EditorPreviewSelected();
    }

    public void EditorPreviewRevealOne()
    {
        previewVisibleFrames = 1;
        EditorPreviewSelected();
    }

    public void EditorPreviewRevealTwo()
    {
        previewVisibleFrames = 2;
        EditorPreviewSelected();
    }

    public void EditorPreviewRevealThree()
    {
        previewVisibleFrames = 3;
        EditorPreviewSelected();
    }

    public void EditorSelectPreviewFrame(int frameIndex)
    {
#if UNITY_EDITOR
        var previewUi = EnsurePreviewUiExists();
        if (previewUi == null) return;

        var proxy = previewUi.GetPreviewFrameProxy(frameIndex);
        if (proxy == null) return;

        Selection.activeGameObject = proxy.gameObject;
        EditorGUIUtility.PingObject(proxy.gameObject);
        SceneView.RepaintAll();
#endif
    }

    public void EditorResetPreviewFrameTransform(int frameIndex)
    {
#if UNITY_EDITOR
        if (previewSequence == null || previewSequence.pages == null) return;
        if (previewPageIndex < 0 || previewPageIndex >= previewSequence.pages.Count) return;
        if (frameIndex < 0 || frameIndex > 2) return;

        Undo.RecordObject(previewSequence, "Reset Comic Frame Transform");
        var page = previewSequence.pages[previewPageIndex];
        var frame = frameIndex == 0 ? page.frame0 : frameIndex == 1 ? page.frame1 : page.frame2;
        frame.imageOffset = Vector2.zero;
        frame.imageScale = Vector2.one;

        if (frameIndex == 0) page.frame0 = frame;
        else if (frameIndex == 1) page.frame1 = frame;
        else page.frame2 = frame;

        previewSequence.pages[previewPageIndex] = page;
        EditorUtility.SetDirty(previewSequence);
        EditorPreviewSelected();
        SceneView.RepaintAll();
#endif
    }

    public void EditorFocusPreview()
    {
#if UNITY_EDITOR
        var previewUi = EnsurePreviewUiExists();
        if (previewUi == null) return;

        if (previewSequence != null && previewSequence.pages != null && previewSequence.pages.Count > 0)
        {
            previewPageIndex = Mathf.Clamp(previewPageIndex, 0, previewSequence.pages.Count - 1);
            previewVisibleFrames = Mathf.Clamp(previewVisibleFrames, 1, 3);
            // ✅ Применяем layout настройки (weights, percent) перед preview!
            ApplyLayoutSizesToUi(previewUi);
            ApplyVisualSettingsToUi(previewUi);
            previewUi.SetPreviewBinding(previewSequence, previewPageIndex);
            previewUi.ShowPreviewPage(previewSequence.pages[previewPageIndex], previewVisibleFrames);
        }

        int focusFrameIndex = Mathf.Clamp(previewVisibleFrames - 1, 0, 2);
        Transform focusTarget = previewUi.GetPreviewFocusTarget(focusFrameIndex);
        if (focusTarget == null) focusTarget = previewUi.transform;

        Selection.activeGameObject = focusTarget.gameObject;
        EditorGUIUtility.PingObject(focusTarget.gameObject);

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.orthographic = true;
            sceneView.in2DMode = true;

            var rt = focusTarget as RectTransform;
            Vector3 worldCenter;
            float halfSize;
            if (rt != null)
            {
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                worldCenter = (corners[0] + corners[2]) * 0.5f;
                float w = Mathf.Abs(corners[2].x - corners[0].x);
                float h = Mathf.Abs(corners[2].y - corners[0].y);
                halfSize = Mathf.Max(w, h) * 0.7f;
            }
            else
            {
                worldCenter = focusTarget.position;
                halfSize = 800f;
            }

            sceneView.pivot = worldCenter;
            sceneView.rotation = Quaternion.Euler(0f, 0f, 0f);
            sceneView.size = Mathf.Max(100f, halfSize);
            sceneView.Repaint();
        }

        SceneView.RepaintAll();
#endif
    }

    private ComicCutsceneUI EnsurePreviewUiExists()
    {
        var previewUi = GetEditorPreviewUi();
        if (previewUi != null)
        {
            previewUi.EnsureBuiltForPreview();
            return previewUi;
        }

#if UNITY_EDITOR
        var go = new GameObject("ComicCutscenePreviewUI");
        go.hideFlags = HideFlags.DontSaveInEditor;
        _editorPreviewUi = go.AddComponent<ComicCutsceneUI>();
        _editorPreviewUi.buildIfMissingInPlayMode = true;
        ApplyVisualSettingsToUi(_editorPreviewUi);
        ApplyLayoutSizesToUi(_editorPreviewUi);
        _editorPreviewUi.EnsureBuiltForPreview();
        return _editorPreviewUi;
#else
        return null;
#endif
    }

    private void ApplyVisualSettingsToUi()
    {
        ApplyVisualSettingsToUi(ui);
    }

    private void ApplyVisualSettingsToUi(ComicCutsceneUI targetUi)
    {
        if (targetUi == null) return;
        targetUi.frameBorderPixels = frameBorderPixels;
        targetUi.textPlateAlpha = textPlateAlpha;
        targetUi.frameTextFontOverride = frameTextFontOverride;
        targetUi.frameTextFontSizeOverride = frameTextFontSizeOverride;
        ApplyLayoutSizesToUi(targetUi);
    }

    private void ApplyLayoutSizesToUi(ComicCutsceneUI targetUi)
    {
        if (targetUi == null) return;
        targetUi.statusHeaderHeightPx = Mathf.Max(0f, statusHeaderHeightPx);
        targetUi.statusHeaderTopPaddingPx = Mathf.Max(0f, statusHeaderTopPaddingPx);
        targetUi.statusHeaderHorizontalPaddingPx = Mathf.Max(0f, statusHeaderHorizontalPaddingPx);
        targetUi.qteBottomHeightPx = Mathf.Max(0f, qteBottomHeightPx);
        targetUi.qteBottomBottomPaddingPx = Mathf.Max(0f, qteBottomBottomPaddingPx);
        targetUi.qteBottomHorizontalPaddingPx = Mathf.Max(0f, qteBottomHorizontalPaddingPx);
        targetUi.matrixZoom = Mathf.Clamp(matrixZoom, 0.5f, 1.8f);
        targetUi.matrixHeightPercentOfAvailable = Mathf.Clamp01(matrixHeightPercentOfAvailable);
        targetUi.matrixFrame0Weight = Mathf.Max(0.1f, matrixFrame0Weight);
        targetUi.matrixRightColumnWeight = Mathf.Max(0.1f, matrixRightColumnWeight);
        targetUi.matrixHorizontalSpacingPx = Mathf.Max(0f, matrixHorizontalSpacingPx);
        targetUi.matrixVerticalSpacingPx = Mathf.Max(0f, matrixVerticalSpacingPx);
    }

    private ComicCutsceneUI GetEditorPreviewUi()
    {
#if UNITY_EDITOR
        if (_editorPreviewUi != null) return _editorPreviewUi;

        ComicCutsceneUI found = null;
        var allPreviewUis = Resources.FindObjectsOfTypeAll<ComicCutsceneUI>();
        for (int i = 0; i < allPreviewUis.Length; i++)
        {
            var candidate = allPreviewUis[i];
            if (candidate == null) continue;
            if (candidate.gameObject == null) continue;
            if (candidate.gameObject.name != "ComicCutscenePreviewUI") continue;

            if (found == null)
            {
                found = candidate;
                continue;
            }

            DestroyImmediate(candidate.gameObject);
        }

        _editorPreviewUi = found;
        return _editorPreviewUi;
#else
        return null;
#endif
    }
}
