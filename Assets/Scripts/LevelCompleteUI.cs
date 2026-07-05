using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCompleteUI : MonoBehaviour
{
    private bool _levelWasCompleted;

    public void MarkLevelCompleted()
    {
        _levelWasCompleted = true;
    }

    private void OnEnable()
    {
        // Keep the current flag state if this is the real finish screen being shown.
    }

    private void OnDisable()
    {
        _levelWasCompleted = false;
    }

    public void NextLevel()
    {
        // Загружаем следующую сцену по индексу
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            if (GameFlowState.CurrentMode == GameFlowMode.Story)
            {
                StorySaveManager.SaveLevelResume(nextSceneIndex);
            }
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No more levels!");
            // Или загрузить меню
            SceneManager.LoadScene(0);
        }
    }

    public void RestartLevel()
    {
        if (GameFlowState.CurrentMode == GameFlowMode.Story)
        {
            StorySaveManager.SaveLevelResume(SceneManager.GetActiveScene().buildIndex);
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        if (GameFlowState.CurrentMode == GameFlowMode.Story)
        {
            if (_levelWasCompleted)
            {
                int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
                if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
                {
                    StorySaveManager.SaveLevelResume(nextSceneIndex);
                }
            }
            else
            {
                StorySaveManager.SaveLevelResume(SceneManager.GetActiveScene().buildIndex);
            }
        }
        SceneManager.LoadScene(0);
    }
}
