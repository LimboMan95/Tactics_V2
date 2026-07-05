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
        var save = StorySaveManager.LoadOrCreate();

        if (comicCutsceneSystem == null)
        {
            comicCutsceneSystem = FindObjectOfType<ComicCutsceneSystem>(true);
        }

        if (save.resumeType == StoryResumeType.Comic)
        {
            if (comicCutsceneSystem != null &&
                comicCutsceneSystem.TryPlayTrigger(save.comicTriggerAfterLevelIndex, save.nextSceneBuildIndexAfterComic, true))
            {
                return;
            }

            if (save.nextSceneBuildIndexAfterComic >= 0)
            {
                SceneManager.LoadScene(save.nextSceneBuildIndexAfterComic);
                return;
            }
        }

        if (save.resumeType == StoryResumeType.Level && save.levelBuildIndex >= 0)
        {
            SceneManager.LoadScene(save.levelBuildIndex);
            return;
        }

        StorySaveManager.SaveComicResume(0, firstGameplayLevelBuildIndex);
        if (comicCutsceneSystem != null && comicCutsceneSystem.TryPlayTrigger(0, firstGameplayLevelBuildIndex, true))
        {
            return;
        }

        StorySaveManager.SaveLevelResume(firstGameplayLevelBuildIndex);
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

    public void DeleteStorySave()
    {
        StorySaveManager.DeleteSave();
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
