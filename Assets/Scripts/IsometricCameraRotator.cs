using UnityEngine;
using System.Collections;

public class IsometricCameraRotator : MonoBehaviour
{
    [Header("Fixed Look Point")]
    public Vector3 groundLookPoint = Vector3.zero;
    public bool useFixedPoint = true;
    
    [Header("Target Object")]
    public Transform lookAtTarget;
    
    [Header("Camera Orbit Settings")]
    public float orbitRadius = 15f;
    
    [Tooltip("Текущий вертикальный угол (читается из Transform)")]
    [SerializeField] private float currentVerticalAngle;
    
    [SerializeField] private float currentHorizontalAngle = 180f;
    
    [Header("Animation")]
    public float rotationDuration = 0.5f;
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Input")]
    public bool allowKeyboardInput = true;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    
    [Header("Debug")]
    [SerializeField] private bool isRotating = false;
    [SerializeField] private float targetHorizontalAngle = 45f;

    public float CurrentHorizontalAngle => currentHorizontalAngle;
    public float CurrentVerticalAngle => currentVerticalAngle;
    
    void Start()
    {
        // ЧИТАЕМ текущий вертикальный угол из Transform камеры
        currentVerticalAngle = transform.eulerAngles.x;
        
        // Если угол больше 180, корректируем (Unity хранит углы 0-360)
        if (currentVerticalAngle > 180f)
            currentVerticalAngle -= 360f;
        
        Debug.Log($"Начальный вертикальный угол камеры: {currentVerticalAngle}°");
        
        if (!useFixedPoint && lookAtTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) lookAtTarget = player.transform;
        }
        
        UpdateCameraPosition();
    }
    
    void Update()
    {
        if (!IsInGameMode()) return;
        
        if (allowKeyboardInput && !isRotating)
        {
            if (Input.GetKeyDown(rotateLeftKey)) RotateLeft();
            else if (Input.GetKeyDown(rotateRightKey)) RotateRight();
        }
    }
    
    void LateUpdate()
    {
        if (!isRotating)
        {
            UpdateCameraRotation();
        }
    }
    
    public void RotateLeft()
    {
        if (!IsInGameMode() || isRotating) return;
        targetHorizontalAngle = currentHorizontalAngle - 90f;
        StartCoroutine(RotateCameraRoutine());
    }
    
    public void RotateRight()
    {
        if (!IsInGameMode() || isRotating) return;
        targetHorizontalAngle = currentHorizontalAngle + 90f;
        StartCoroutine(RotateCameraRoutine());
    }
    
    private IEnumerator RotateCameraRoutine()
    {
        isRotating = true;
        
        float startAngle = currentHorizontalAngle;
        Vector3 lookPoint = useFixedPoint ? groundLookPoint : 
                          (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
        
        float elapsedTime = 0f;
        
        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / rotationDuration;
            float curvedProgress = rotationCurve.Evaluate(progress);
            
            currentHorizontalAngle = Mathf.Lerp(startAngle, targetHorizontalAngle, curvedProgress);
            
            // Обновляем позицию и вращение
            UpdateCameraPosition(lookPoint);
            UpdateCameraRotation();
            
            yield return null;
        }
        
        currentHorizontalAngle = targetHorizontalAngle;
        UpdateCameraPosition(lookPoint);
        UpdateCameraRotation();
        
        isRotating = false;
    }
    
    private void UpdateCameraPosition(Vector3 center)
    {
        float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
        
        // Вычисляем горизонтальную позицию
        float xPos = center.x + Mathf.Sin(horizontalRad) * orbitRadius;
        float zPos = center.z + Mathf.Cos(horizontalRad) * orbitRadius;
        
        // Вычисляем вертикальную позицию с учетом угла наклона
        float horizontalDistance = orbitRadius;
        float yPos = center.y + Mathf.Tan(currentVerticalAngle * Mathf.Deg2Rad) * horizontalDistance;
        
        transform.position = new Vector3(xPos, yPos, zPos);
    }
    
    private void UpdateCameraPosition()
    {
        Vector3 center = useFixedPoint ? groundLookPoint : 
                        (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
        UpdateCameraPosition(center);
    }

    public Vector3 GetLookAtPoint()
{
    return useFixedPoint ? groundLookPoint : 
           (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
}
    
    private void UpdateCameraRotation()
    {
        Vector3 lookPoint = useFixedPoint ? groundLookPoint : 
                          (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
        
        // Вычисляем направление к точке
        Vector3 directionToTarget = lookPoint - transform.position;
        
        // Создаем вращение с нужным вертикальным углом
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        
        // РАЗДЕЛЯЕМ УГЛЫ: сохраняем вертикальный, меняем только горизонтальный
        Vector3 euler = targetRotation.eulerAngles;
        
        // Сохраняем наш фиксированный вертикальный угол
        euler.x = currentVerticalAngle;
        
        // Корректируем если угол > 180
        if (euler.x > 180f) euler.x -= 360f;
        
        transform.rotation = Quaternion.Euler(euler);
    }
    
    private bool IsInGameMode() { return true; }
    
    // Метод для проверки текущих углов
    public void DebugCameraAngles()
    {
        Vector3 euler = transform.eulerAngles;
        float vertical = euler.x;
        if (vertical > 180f) vertical -= 360f;
        
        Debug.Log($"Камера: Горизонт={currentHorizontalAngle:F1}°, Вертикаль={vertical:F1}°");
    }
}