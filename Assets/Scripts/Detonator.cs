using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Detonator : MonoBehaviour, IResettable
{
    [Header("Цвет")]
    public Color detonatorColor = Color.magenta;
    
    [Header("Настройки")]
    public float pressDepth = 0.1f;
    public float centerThreshold = 0.3f;
    
    [Header("События")]
    public UnityEvent onPressed;
    
    private bool isPressed = false;
    private Renderer detonatorRenderer;
    private Color originalColor;
    
    // Для ресета
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private GridObjectMover editModeChecker;
    
    void Start()
    {
        detonatorRenderer = GetComponent<Renderer>();
        originalColor = detonatorRenderer.material.color;
        
        // Запоминаем начальную позицию
        initialPosition = transform.position;
        initialRotation = transform.rotation;
          // Находим editModeChecker
    editModeChecker = FindObjectOfType<GridObjectMover>();
    }
    
    void OnTriggerStay(Collider other)
{
    // Не работаем в режиме редактирования
    if (editModeChecker != null && editModeChecker.isInEditMode) 
        return;
    
    if (isPressed) return;
    
    DickControlledCube cube = other.GetComponent<DickControlledCube>();
    if (cube == null) return;
    
    Vector3 cubePos = cube.transform.position;
    Vector3 detPos = transform.position;
    
    float deltaX = Mathf.Abs(cubePos.x - detPos.x);
    float deltaZ = Mathf.Abs(cubePos.z - detPos.z);
    
    if (deltaX <= centerThreshold && deltaZ <= centerThreshold)
    {
        Press();
    }
}
    
   void Press()
{
    isPressed = true;
    StartCoroutine(PressAnimation());
    onPressed?.Invoke();
    // Не уничтожаем!
}
    
    IEnumerator PressAnimation()
{
    Vector3 startPos = transform.position;  // Текущая позиция
    Vector3 pressedPos = startPos + Vector3.down * pressDepth;
    float duration = 0.2f;
    float elapsed = 0f;
    
    while (elapsed < duration)
    {
        transform.position = Vector3.Lerp(startPos, pressedPos, elapsed / duration);
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
    
   public void ResetObject()
{
    Debug.Log($"🔵 Detonator Reset START: isPressed={isPressed}, pos={transform.position}");
    
    isPressed = false;
    
    // ВОЗВРАЩАЕМ ТОЛЬКО ВЫСОТУ!
    Vector3 pos = transform.position;
    pos.y = initialPosition.y;
    transform.position = pos;
    
    StopAllCoroutines();
    
    if (detonatorRenderer != null)
        detonatorRenderer.material.color = originalColor;
    
    Debug.Log($"🟢 Detonator Reset END: isPressed={isPressed}, pos={transform.position}");
}
}