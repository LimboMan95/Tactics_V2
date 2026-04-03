using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectButton : MonoBehaviour
{
    public int sceneBuildIndex = 1;

    public void Load()
    {
        if (sceneBuildIndex < 0 || sceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Scene with index {sceneBuildIndex} does not exist in Build Settings!");
            return;
        }

        SceneManager.LoadScene(sceneBuildIndex);
    }
}

