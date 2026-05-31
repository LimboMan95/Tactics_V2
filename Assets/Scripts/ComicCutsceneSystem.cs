using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("Sequences")]
    public List<ComicSequence> sequences = new List<ComicSequence>();

    [Header("UI")]
    public ComicCutsceneUI ui;

    private bool _playing;
    private static readonly HashSet<int> _playedTriggersThisSession = new HashSet<int>();
    private static ComicCutsceneSystem _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (ui == null) ui = FindObjectOfType<ComicCutsceneUI>(true);
        if (ui != null)
        {
            ui.frameBorderPixels = frameBorderPixels;
            ui.textPlateAlpha = textPlateAlpha;
            ui.frameTextFontOverride = frameTextFontOverride;
            ui.frameTextFontSizeOverride = frameTextFontSizeOverride;
            ui.Initialize(fadeDuration);
            ui.RefreshBorders();
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
    }

    public bool TryPlayAfterLevel(int completedLevelBuildIndex, int nextSceneBuildIndex)
    {
        if (_playing) return true;

        var matches = CollectSequencesForTrigger(completedLevelBuildIndex);
        if (matches.Count == 0) return false;

        PlaySequences(matches, nextSceneBuildIndex, loadSceneOnFinish: true);
        return true;
    }

    public bool TryPlayTrigger(int triggerAfterLevelIndex, int nextSceneBuildIndex, bool loadSceneOnFinish)
    {
        if (_playing) return true;

        var matches = CollectSequencesForTrigger(triggerAfterLevelIndex);
        if (matches.Count == 0) return false;

        PlaySequences(matches, nextSceneBuildIndex, loadSceneOnFinish);
        return true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoPlayBeforeFirstLevel();
    }

    private void TryAutoPlayBeforeFirstLevel()
    {
        if (_playing) return;
        if (!playBeforeFirstLevel) return;
        if (SceneManager.GetActiveScene().buildIndex != firstGameplayLevelBuildIndex) return;
        if (_playedTriggersThisSession.Contains(0)) return;

        var matches = CollectSequencesForTrigger(0);
        if (matches.Count == 0) return;

        _playedTriggersThisSession.Add(0);
        PlaySequences(matches, nextSceneBuildIndex: firstGameplayLevelBuildIndex, loadSceneOnFinish: false);
    }

    private List<ComicSequence> CollectSequencesForTrigger(int triggerAfterLevelIndex)
    {
        var result = new List<ComicSequence>();
        for (int i = 0; i < sequences.Count; i++)
        {
            var seq = sequences[i];
            if (seq == null) continue;
            if (seq.triggerAfterLevelIndex != triggerAfterLevelIndex) continue;
            if (seq.pages == null || seq.pages.Count == 0) continue;
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
            ui.Initialize(fadeDuration);
        }
        else
        {
            ui.frameBorderPixels = frameBorderPixels;
            ui.textPlateAlpha = textPlateAlpha;
            ui.frameTextFontOverride = frameTextFontOverride;
            ui.frameTextFontSizeOverride = frameTextFontSizeOverride;
        }
        ui.RefreshBorders();

        if (dontDestroyOnLoad && loadSceneOnFinish && ui != null)
        {
            if (ui.transform.parent != transform)
            {
                ui.transform.SetParent(transform, worldPositionStays: false);
            }
            DontDestroyOnLoad(ui.gameObject);
        }

        _playing = true;
        StartCoroutine(TrackUntilUiFinishes());
        ui.Play(list.ToArray(), nextSceneBuildIndex, loadSceneOnFinish);
    }

    private IEnumerator TrackUntilUiFinishes()
    {
        while (ui != null && ui.gameObject.activeSelf)
        {
            yield return null;
        }
        _playing = false;
    }
}
