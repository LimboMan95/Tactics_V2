using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Detonator : MonoBehaviour
{
    [Header("Цвет")]
    public Color detonatorColor = Color.magenta;
    
    [Header("Настройки")]
    public float pressDepth = 0.2f;
    
    [Header("События")]
    public UnityEvent onPressed;
    
    private bool isPressed = false;
    private Vector3 originalPosition;
    private Renderer detonatorRenderer;
    private Color originalColor;
    
    void Start()
    {
        originalPosition = transform.position;
        detonatorRenderer = GetComponent<Renderer>();
        originalColor = detonatorRenderer.material.color;
    }
    
    // Временно отключаем OnTriggerEnter
    /*
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"🔴 Enter: {other.name}");
        if (isPressed) return;
        
        DickControlledCube cube = other.GetComponent<DickControlledCube>();
        if (cube != null)
        {
            Press();
        }
    }
    */
    
    // Используем только OnTriggerStay для постоянной проверки
    void OnTriggerStay(Collider other)
    {
        if (isPressed) return;
        
        DickControlledCube cube = other.GetComponent<DickControlledCube>();
        if (cube == null) return;
        
        // ПОЛНАЯ ИНФА
        Vector3 cubePos = cube.transform.position;
        Vector3 detPos = transform.position;
        
        Debug.Log($"=== FRAME {Time.frameCount} ===");
        Debug.Log($"📌 Cube pos: ({cubePos.x:F3}, {cubePos.y:F3}, {cubePos.z:F3})");
        Debug.Log($"📌 Det pos:  ({detPos.x:F3}, {detPos.y:F3}, {detPos.z:F3})");
        
        // Разница только по X и Z
        float deltaX = Mathf.Abs(cubePos.x - detPos.x);
        float deltaZ = Mathf.Abs(cubePos.z - detPos.z);
        
        Debug.Log($"📏 Delta X: {deltaX:F3}, Delta Z: {deltaZ:F3}");
        
        // Цель - оказаться в пределах 0.3 по каждой оси
        bool isCentered = deltaX <= 0.3f && deltaZ <= 0.3f;
        Debug.Log($"🎯 Is centered: {isCentered} (threshold 0.3)");
        
        if (isCentered)
        {
            Debug.Log("💥 CENTER REACHED! PRESSING!");
            Press();
        }
    }
    
    void Press()
    {
        isPressed = true;
        StartCoroutine(PressAnimation());
        onPressed?.Invoke();
    }
    
    IEnumerator PressAnimation()
    {
        Vector3 pressedPos = originalPosition - Vector3.up * pressDepth;
        float duration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(originalPosition, pressedPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.position = pressedPos;
    }
    
    public void Highlight(bool highlight)
    {
        if (detonatorRenderer != null)
        {
            detonatorRenderer.material.color = highlight ? detonatorColor : originalColor;
        }
    }
}