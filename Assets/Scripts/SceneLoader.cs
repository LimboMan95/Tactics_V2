using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Публичный метод для загрузки сцены по номеру
    public void LoadSceneByIndex(int sceneIndex)
    {
        // Проверяем, существует ли сцена с таким индексом
        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(sceneIndex);
        }
        else
        {
            Debug.LogError("Scene with index " + sceneIndex + " does not exist in Build Settings!");
        }
    }

    // Альтернативный метод с проверкой через SceneUtility
    public void LoadSceneByIndexSafe(int sceneIndex)
    {
        string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
        
        if (!string.IsNullOrEmpty(scenePath))
        {
            SceneManager.LoadScene(sceneIndex);
        }
        else
        {
            Debug.LogError("Scene with index " + sceneIndex + " is not valid or not in Build Settings!");
        }
    }

    // Метод для кнопки UI (работает со строкой, которую можно преобразовать в число)
    public void LoadSceneByIndexString(string indexString)
    {
        if (int.TryParse(indexString, out int sceneIndex))
        {
            LoadSceneByIndex(sceneIndex);
        }
        else
        {
            Debug.LogError("Invalid scene index: " + indexString);
        }
    }
}