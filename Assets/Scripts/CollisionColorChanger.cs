using UnityEngine;
using System.Collections; // Добавлена эта строка

[RequireComponent(typeof(Rigidbody), typeof(MeshRenderer))]
public class CollisionColorChanger : MonoBehaviour
{
    [Header("Collision Settings")]
    [Tooltip("Тег объектов, при столкновении с которыми куб реагирует")]
    public string obstacleTag = "Obstacle";
    
    [Tooltip("Цвет куба при столкновении")]
    public Color collisionColor = Color.red;
    
    [Tooltip("Время в секундах до возврата исходного цвета")]
    public float colorResetDelay = 2f;

    private MeshRenderer meshRenderer;
    private Color originalColor;
    private bool isInCollisionState = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        originalColor = meshRenderer.material.color;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isInCollisionState && collision.gameObject.CompareTag(obstacleTag))
        {
            StartCollisionEffect();
        }
    }

    private void StartCollisionEffect()
    {
        isInCollisionState = true;
        meshRenderer.material.color = collisionColor;
        Invoke("ResetCollisionEffect", colorResetDelay);
        
        // Альтернативный вариант с плавным изменением:
        // StartCoroutine(SmoothColorTransition(collisionColor, 0.5f));
    }

    private void ResetCollisionEffect()
    {
        meshRenderer.material.color = originalColor;
        isInCollisionState = false;
        
        // Альтернативный вариант с плавным изменением:
        // StartCoroutine(SmoothColorTransition(originalColor, 0.5f));
    }

    private IEnumerator SmoothColorTransition(Color targetColor, float duration)
    {
        Color startColor = meshRenderer.material.color;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            meshRenderer.material.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        meshRenderer.material.color = targetColor;
    }
}