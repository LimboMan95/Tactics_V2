using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuModeRouter : MonoBehaviour
{
    public GameObject rootModePanel;
    public GameObject levelSelectPanel;
    public ComicCutsceneSystem comicCutsceneSystem;
    public int firstGameplayLevelBuildIndex = 1;

    private void OnEnable()
    {
        ShowModeSelection();
    }

    public void StartStory()
    {
        GameFlowState.SetStoryMode();

        if (comicCutsceneSystem == null)
        {
            comicCutsceneSystem = FindObjectOfType<ComicCutsceneSystem>(true);
        }

        if (comicCutsceneSystem != null && comicCutsceneSystem.TryPlayTrigger(0, firstGameplayLevelBuildIndex, true))
        {
            return;
        }

        SceneManager.LoadScene(firstGameplayLevelBuildIndex);
    }

    public void OpenLevelSelect()
    {
        GameFlowState.SetLevelSelectMode();
        SetPanels(showModePanel: false, showLevelSelectPanel: true);
    }

    public void BackToModeSelection()
    {
        ShowModeSelection();
    }

    public void ShowModeSelection()
    {
        SetPanels(showModePanel: true, showLevelSelectPanel: false);
    }

    private void SetPanels(bool showModePanel, bool showLevelSelectPanel)
    {
        if (rootModePanel != null) rootModePanel.SetActive(showModePanel);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(showLevelSelectPanel);
    }
}

