using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class LevelSelectGrid : MonoBehaviour
{
    public Button buttonPrefab;
    public RectTransform parentPanel;

    public int firstLevelBuildIndex = 1;
    public int levelCount = 0;

    public Vector2 spacing = new Vector2(10f, 10f);
    public Vector2 padding = new Vector2(10f, 10f);

    public bool clearExisting = true;
    public bool autoRebuildInEditMode = false;
    public bool rebuildOnStartInPlayMode = true;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private void OnEnable()
    {
        if (!Application.isPlaying && autoRebuildInEditMode)
        {
            Rebuild();
        }
    }

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStartInPlayMode)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (buttonPrefab == null || parentPanel == null) return;

        if (clearExisting)
        {
            for (int i = parentPanel.childCount - 1; i >= 0; i--)
            {
                var child = parentPanel.GetChild(i);
                if (child == null) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            _spawned.Clear();
        }

        int totalLevels = levelCount;
        if (totalLevels <= 0)
        {
            totalLevels = Mathf.Max(0, SceneManager.sceneCountInBuildSettings - firstLevelBuildIndex);
        }

        RectTransform prefabRt = buttonPrefab.GetComponent<RectTransform>();
        Vector2 cellSize = prefabRt != null ? prefabRt.rect.size : new Vector2(160f, 60f);
        if (cellSize.x <= 1e-3f) cellSize.x = Mathf.Abs(prefabRt.sizeDelta.x);
        if (cellSize.y <= 1e-3f) cellSize.y = Mathf.Abs(prefabRt.sizeDelta.y);
        if (cellSize.x <= 1e-3f) cellSize.x = 160f;
        if (cellSize.y <= 1e-3f) cellSize.y = 60f;

        float panelWidth = parentPanel.rect.width;
        if (panelWidth <= 1e-3f) panelWidth = parentPanel.sizeDelta.x;
        if (panelWidth <= 1e-3f) panelWidth = 800f;

        float availableWidth = panelWidth - (padding.x * 2f);
        int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + spacing.x) / (cellSize.x + spacing.x)));

        for (int i = 0; i < totalLevels; i++)
        {
            int levelNumber = i + 1;
            int buildIndex = firstLevelBuildIndex + i;

            Button b = Instantiate(buttonPrefab, parentPanel);
            b.name = $"{buttonPrefab.name}_{levelNumber}";

            var levelButton = b.GetComponent<LevelSelectButton>();
            if (levelButton == null) levelButton = b.gameObject.AddComponent<LevelSelectButton>();
            levelButton.sceneBuildIndex = buildIndex;

            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(levelButton.Load);

            SetButtonLabel(b.gameObject, levelNumber.ToString());

            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                int row = i / columns;
                int col = i % columns;
                float x = padding.x + col * (cellSize.x + spacing.x);
                float y = -padding.y - row * (cellSize.y + spacing.y);
                rt.anchoredPosition = new Vector2(x, y);
            }

            _spawned.Add(b.gameObject);
        }
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        if (parentPanel == null) return;
        for (int i = parentPanel.childCount - 1; i >= 0; i--)
        {
            var child = parentPanel.GetChild(i);
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        _spawned.Clear();
    }

    private static void SetButtonLabel(GameObject buttonGo, string text)
    {
        if (buttonGo == null) return;

        TMP_Text tmp = buttonGo.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text legacy = buttonGo.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = text;
    }
}
