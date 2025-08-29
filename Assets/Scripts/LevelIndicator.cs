using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelIndicator : MonoBehaviour
{
    public TextMeshProUGUI levelText;
    public string baseText = "Level ";

    void Start()
    {
        // Получаем индекс текущей сцены
        int currentLevelIndex = SceneManager.GetActiveScene().buildIndex;
        
        // Обновляем текст в UI
        if (levelText != null)
        {
            levelText.text = baseText + (currentLevelIndex);
        }
    }
}