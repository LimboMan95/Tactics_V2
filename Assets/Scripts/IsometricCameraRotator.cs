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
    public float minOrbitRadius = 9f;    // Минимальное расстояние (близко)
    public float maxOrbitRadius = 15f;   // Максимальное расстояние (далеко)
    [SerializeField] private float orbitRadius = 15f;
    
    [Tooltip("Текущий вертикальный угол (читается из Transform)")]
    [SerializeField] private float currentVerticalAngle;
    
    [SerializeField] private float currentHorizontalAngle = 180f;
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float touchZoomSensitivity = 0.5f; // Чувствительность зума щипком
    public float zoomSmoothTime = 0.2f;
    private float targetOrbitRadius;
    private float zoomVelocity;
    
    [Header("Scroll Settings")]
    public float scrollSpeed = 10f;
    public float touchScrollSensitivity = 2f; // Чувствительность тач-скролла
    public bool enableScroll = true;
    public bool enableEdgeScroll = false; // Скролл при подъезде к границам экрана
    public Vector2 scrollBounds = new Vector2(50f, 50f); // Границы скролла по X и Z
    public float maxZoomForScroll = 20f; // При каком зуме скролл включается (меньше = ближе)
    [SerializeField] private Vector3 scrollOffset = Vector3.zero; // Текущее смещение от центра
    
    [Header("Touch Input")]
    public bool enableTouchInput = true;
    public float touchDeadZone = 10f; // Минимальное движение для начала скролла (в пикселях)
    private Vector2 lastTouchPosition;
    private bool isTouching = false;
    private float initialTouchDistance;
    private bool isPinching = false;
    
    [Header("Mouse Drag Scroll")]
    public bool enableMouseDrag = true;
    public float mouseDragSensitivity = 1.5f;
    private bool isMouseDragging = false;
    private Vector2 lastMousePosition;
    
    [Header("Rotation Pivot Settings")]
    public bool useAdaptiveRotationPivot = true; // Вращение вокруг текущей точки середины экрана
    public float adaptivePivotTransitionSpeed = 5f; // Скорость перехода к адаптивной точке
    private Vector3 adaptiveRotationPivot; // Текущая адаптивная точка вращения
    private Vector3 targetAdaptivePivot; // Целевая адаптивная точка
    private bool isAdaptingPivot = false;
    
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
    public float CurrentOrbitRadius => orbitRadius;
    
    void Start()
{
    // ЧИТАЕМ текущий вертикальный угол из Transform камеры
    currentVerticalAngle = transform.eulerAngles.x;
    
    // Если угол больше 180, корректируем (Unity хранит углы 0-360)
    if (currentVerticalAngle > 180f)
        currentVerticalAngle -= 360f;
    
    // ВЫЧИСЛЯЕМ начальный горизонтальный угол из позиции камеры
    Vector3 lookPoint = GetCurrentLookPoint();
    Vector3 cameraToCenter = lookPoint - transform.position;  // Вектор от камеры к центру
    cameraToCenter.y = 0;
    
    if (cameraToCenter.magnitude > 0.01f)
    {
        // Нормализуем вектор
        cameraToCenter.Normalize();
        
        // Вычисляем угол вектора (куда смотрит камера - к центру)
        // atan2(x, z) дает угол между осью Z+ и вектором
        float angle = Mathf.Atan2(cameraToCenter.x, cameraToCenter.z) * Mathf.Rad2Deg;
        
        currentHorizontalAngle = angle;
        targetHorizontalAngle = angle;
        
        Debug.Log($"Вычислен начальный горизонтальный угол: {currentHorizontalAngle:F1}°");
        Debug.Log($"Вектор камера->центр: {cameraToCenter}, длина: {cameraToCenter.magnitude}");
    }
    else
    {
        Debug.LogWarning("Камера слишком близко к точке взгляда");
        // По умолчанию 180° (смотрит на юг)
        currentHorizontalAngle = 180f;
        targetHorizontalAngle = 180f;
    }
    
    // Инициализируем таргет зума текущим радиусом
    targetOrbitRadius = orbitRadius;
    
    // Инициализируем адаптивную точку вращения
    adaptiveRotationPivot = GetCurrentLookPoint();
    targetAdaptivePivot = adaptiveRotationPivot;
    
    Debug.Log($"Старт: вертикаль={currentVerticalAngle}°, горизонт={currentHorizontalAngle}°, радиус={orbitRadius}");
    
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
        
        bool cameraNeedsUpdate = false;
        
        // Обработка зума (колесико мыши + тач)
        cameraNeedsUpdate = HandleZoom() || cameraNeedsUpdate;
        
        // Обработка скролла (только если камера достаточно близко)
        if (enableScroll && orbitRadius <= maxZoomForScroll)
        {
            cameraNeedsUpdate = HandleKeyboardScroll() || cameraNeedsUpdate;
            cameraNeedsUpdate = HandleMouseDragScroll() || cameraNeedsUpdate;
            cameraNeedsUpdate = HandleTouchScroll() || cameraNeedsUpdate;
        }
        
        // Обновляем адаптивную точку вращения
        if (useAdaptiveRotationPivot)
        {
            UpdateAdaptivePivot();
        }
        
        // Обновляем позицию камеры если были изменения
        if (cameraNeedsUpdate && !isRotating)
        {
            UpdateCameraPosition();
        }
        
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

    public void ResetCameraToDefaultSmooth(float duration = 1f)
{
    StartCoroutine(ResetCameraRoutine(duration));
}

private IEnumerator ResetCameraRoutine(float duration)
{
    // Запоминаем начальные значения
    float startRadius = targetOrbitRadius;
    float targetRadius = maxOrbitRadius;
    
    float startAngle = currentHorizontalAngle;
    float targetAngle = 0f; // ← Или твой изначальный угол
    
    Vector3 startScroll = scrollOffset;
    Vector3 targetScroll = Vector3.zero;
    
    Debug.Log($"Начало сброса: старт зум={startRadius}, цель зум={targetRadius}, угол {startAngle}°→{targetAngle}°");
    
    float elapsedTime = 0f;
    
    while (elapsedTime < duration)
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / duration;
        float curvedProgress = Mathf.SmoothStep(0f, 1f, progress);
        
        // Плавно интерполируем ТАРГЕТ зума
        targetOrbitRadius = Mathf.Lerp(startRadius, targetRadius, curvedProgress);
        
        // Плавно интерполируем угол
        currentHorizontalAngle = Mathf.LerpAngle(startAngle, targetAngle, curvedProgress);
        
        // Плавно интерполируем скролл
        scrollOffset = Vector3.Lerp(startScroll, targetScroll, curvedProgress);
        
        // Обновляем позицию камеры
        UpdateCameraPosition();
        
        yield return null;
    }
    
    // Финальные значения
    orbitRadius = targetRadius;
    targetOrbitRadius = targetRadius;
    zoomVelocity = 0f;
    
    currentHorizontalAngle = targetAngle;
    targetHorizontalAngle = targetAngle;
    
    scrollOffset = targetScroll;
    
    // Сбрасываем адаптивную точку
    if (useAdaptiveRotationPivot)
    {
        adaptiveRotationPivot = GetCurrentLookPoint();
        targetAdaptivePivot = adaptiveRotationPivot;
        isAdaptingPivot = false;
    }
    
    // Финальное обновление
    UpdateCameraPosition();
    UpdateCameraRotation();
    
    Debug.Log($"Камера плавно сброшена: зум={orbitRadius}, угол={currentHorizontalAngle}°, скролл={scrollOffset}");
}
    
    private bool HandleZoom()
    {
        bool changed = false;
        
        // Зум колесиком мыши
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            targetOrbitRadius -= scrollInput * zoomSpeed;
            targetOrbitRadius = Mathf.Clamp(targetOrbitRadius, minOrbitRadius, maxOrbitRadius);
            changed = true;
            
            // При сильном отдалении сбрасываем скролл
            if (targetOrbitRadius >= maxOrbitRadius * 0.9f)
            {
                //scrollOffset = Vector3.zero;
            }
            
            // Обновляем адаптивную точку при зуме
            if (useAdaptiveRotationPivot)
            {
                UpdateTargetAdaptivePivotFromMouse();
            }
        }
        
        // Зум щипком (тач)
        if (enableTouchInput)
        {
            changed = HandlePinchZoom() || changed;
        }
        
        // Плавный зум
        orbitRadius = Mathf.SmoothDamp(orbitRadius, targetOrbitRadius, ref zoomVelocity, zoomSmoothTime);
        
        return changed;
    }
    
    private void UpdateTargetAdaptivePivotFromMouse()
{
    // Если уже есть скролл - не меняем адаптивную точку при зуме
    // Камера будет зумить вокруг текущей точки скролла
    if (scrollOffset.magnitude > 0.1f)
    {
        return; // Просто выходим, ничего не делаем
    }
    
    // Только если скролла нет - обновляем адаптивную точку под курсором
    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
    
    if (groundPlane.Raycast(ray, out float distance))
    {
        Vector3 pointOnGround = ray.GetPoint(distance);
        targetAdaptivePivot = pointOnGround;
        isAdaptingPivot = true;
    }
}
    
    private bool HandlePinchZoom()
    {
        bool changed = false;
        
        // Проверяем мультитач (2 касания)
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            
            // Если только начали щипок
            if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
            {
                initialTouchDistance = Vector2.Distance(touch1.position, touch2.position);
                isPinching = true;
                
                // Обновляем адаптивную точку для тач-устройств
                if (useAdaptiveRotationPivot)
                {
                    Vector2 midpoint = (touch1.position + touch2.position) * 0.5f;
                    UpdateTargetAdaptivePivotFromScreenPoint(midpoint);
                }
            }
            // Если двигаем пальцы
            else if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
            {
                if (isPinching)
                {
                    float currentDistance = Vector2.Distance(touch1.position, touch2.position);
                    float distanceDelta = currentDistance - initialTouchDistance;
                    
                    // Применяем зум с чувствительностью
                    targetOrbitRadius -= distanceDelta * touchZoomSensitivity * 0.01f;
                    targetOrbitRadius = Mathf.Clamp(targetOrbitRadius, minOrbitRadius, maxOrbitRadius);
                    changed = true;
                    
                    initialTouchDistance = currentDistance;
                }
            }
            // Если закончили щипок
            else if (touch1.phase == TouchPhase.Ended || touch2.phase == TouchPhase.Ended)
            {
                isPinching = false;
            }
        }
        else
        {
            isPinching = false;
        }
        
        return changed;
    }
    
    private void UpdateTargetAdaptivePivotFromScreenPoint(Vector2 screenPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPoint);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 pointOnGround = ray.GetPoint(distance);
            targetAdaptivePivot = pointOnGround;
            isAdaptingPivot = true;
        }
    }
    
    private void UpdateAdaptivePivot()
    {
        if (isAdaptingPivot)
        {
            // Плавно двигаем адаптивную точку к цели
            adaptiveRotationPivot = Vector3.Lerp(adaptiveRotationPivot, targetAdaptivePivot, 
                Time.deltaTime * adaptivePivotTransitionSpeed);
            
            // Если достаточно близко, останавливаемся
            if (Vector3.Distance(adaptiveRotationPivot, targetAdaptivePivot) < 0.1f)
            {
                adaptiveRotationPivot = targetAdaptivePivot;
                isAdaptingPivot = false;
            }
        }
        else
        {
            // Если не адаптируемся, используем текущую точку взгляда
            adaptiveRotationPivot = GetCurrentLookPoint();
            targetAdaptivePivot = adaptiveRotationPivot;
        }
    }
    
    private bool HandleKeyboardScroll()
    {
        bool changed = false;
        Vector3 scrollInput = Vector3.zero;
        
        // Клавиши WASD/стрелки
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            scrollInput.z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            scrollInput.z -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            scrollInput.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            scrollInput.x += 1f;
        
        // Края экрана мышью (для ПК) - ТОЛЬКО ЕСЛИ ВКЛЮЧЕНО
        if (enableEdgeScroll)
        {
            if (Input.mousePosition.x <= 10) scrollInput.x -= 1f;
            if (Input.mousePosition.x >= Screen.width - 10) scrollInput.x += 1f;
            if (Input.mousePosition.y <= 10) scrollInput.z -= 1f;
            if (Input.mousePosition.y >= Screen.height - 10) scrollInput.z += 1f;
        }
        
        // Нормализуем если диагональное движение
        if (scrollInput.magnitude > 1f)
            scrollInput.Normalize();
        
        // Применяем скролл
        if (scrollInput.magnitude > 0.01f)
        {
            ApplyScrollOffset(scrollInput * scrollSpeed * Time.deltaTime);
            changed = true;
            
            // При скролле сбрасываем адаптивную точку
            if (useAdaptiveRotationPivot)
            {
                isAdaptingPivot = false;
                adaptiveRotationPivot = GetCurrentLookPoint();
                targetAdaptivePivot = adaptiveRotationPivot;
            }
        }
        
        return changed;
    }
    
    private bool HandleMouseDragScroll()
    {
        if (!enableMouseDrag) return false;
        
        bool changed = false;
        
        // Начало drag (зажата правая кнопка мыши или средняя)
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isMouseDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        
        // Во время drag
        if (isMouseDragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
        {
            Vector2 currentMousePos = Input.mousePosition;
            Vector2 delta = currentMousePos - lastMousePosition;
            
            // Если движение достаточно большое (игнорируем микродвижения)
            if (delta.magnitude > touchDeadZone * 0.5f)
            {
                // Инвертируем направление (тянешь вправо - камера движется влево)
                Vector3 scrollInput = new Vector3(-delta.x, 0, -delta.y) * mouseDragSensitivity * 0.01f;
                
                ApplyScrollOffset(scrollInput);
                changed = true;
                
                // При скролле сбрасываем адаптивную точку
                if (useAdaptiveRotationPivot)
                {
                    isAdaptingPivot = false;
                    adaptiveRotationPivot = GetCurrentLookPoint();
                    targetAdaptivePivot = adaptiveRotationPivot;
                }
            }
            
            lastMousePosition = currentMousePos;
        }
        
        // Конец drag
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isMouseDragging = false;
        }
        
        return changed;
    }
    
    private bool HandleTouchScroll()
    {
        if (!enableTouchInput) return false;
        
        bool changed = false;
        
        // Одиночное касание для скролла
        if (Input.touchCount == 1 && !isPinching)
        {
            Touch touch = Input.GetTouch(0);
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    lastTouchPosition = touch.position;
                    isTouching = true;
                    break;
                    
                case TouchPhase.Moved:
                    if (isTouching)
                    {
                        Vector2 delta = touch.position - lastTouchPosition;
                        
                        // Проверяем dead zone
                        if (delta.magnitude > touchDeadZone)
                        {
                            // Инвертируем направление для естественного скролла
                            Vector3 scrollInput = new Vector3(-delta.x, 0, -delta.y) * 
                                                 touchScrollSensitivity * 0.01f;
                            
                            ApplyScrollOffset(scrollInput);
                            changed = true;
                            
                            // При скролле сбрасываем адаптивную точку
                            if (useAdaptiveRotationPivot)
                            {
                                isAdaptingPivot = false;
                                adaptiveRotationPivot = GetCurrentLookPoint();
                                targetAdaptivePivot = adaptiveRotationPivot;
                            }
                        }
                        
                        lastTouchPosition = touch.position;
                    }
                    break;
                    
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouching = false;
                    break;
            }
        }
        else if (Input.touchCount == 0)
        {
            isTouching = false;
        }
        
        return changed;
    }
    
    private void ApplyScrollOffset(Vector3 offsetDelta)
    {
        // Используем фиксированные оси для скролла
        // Создаем направление вперед без вертикальной составляющей
        Vector3 cameraForward = transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();
        
        // Правый вектор всегда горизонтален
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
        
        Vector3 worldOffset = cameraRight * offsetDelta.x + 
                             cameraForward * offsetDelta.z;
        
        // Обнуляем Y компонент (чтобы скроллить только по горизонтали)
        worldOffset.y = 0;
        
        // Добавляем к текущему смещению
        Vector3 newScrollOffset = scrollOffset + worldOffset;
        
        // Ограничиваем границами
        newScrollOffset.x = Mathf.Clamp(newScrollOffset.x, -scrollBounds.x, scrollBounds.x);
        newScrollOffset.z = Mathf.Clamp(newScrollOffset.z, -scrollBounds.y, scrollBounds.y);
        
        scrollOffset = newScrollOffset;
    }
    
    public void RotateLeft()
    {
        if (!IsInGameMode() || isRotating) return;
        targetHorizontalAngle = Mathf.Repeat(currentHorizontalAngle - 90f, 360f);
        StartCoroutine(RotateCameraRoutine());
    }
    
    public void RotateRight()
    {
        if (!IsInGameMode() || isRotating) return;
        targetHorizontalAngle = Mathf.Repeat(currentHorizontalAngle + 90f, 360f);
        StartCoroutine(RotateCameraRoutine());
    }
    
   private IEnumerator RotateCameraRoutine()
{
    isRotating = true;
    
    float startAngle = currentHorizontalAngle;
    
    // Определяем точку вращения
    Vector3 lookPoint;
    if (useAdaptiveRotationPivot)
    {
        lookPoint = adaptiveRotationPivot;
    }
    else
    {
        lookPoint = useFixedPoint ? groundLookPoint : 
                   (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
    }
    
    float elapsedTime = 0f;
    
    while (elapsedTime < rotationDuration)
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / rotationDuration;
        float curvedProgress = rotationCurve.Evaluate(progress);
        
        currentHorizontalAngle = Mathf.LerpAngle(startAngle, targetHorizontalAngle, curvedProgress);
        
        // Обновляем позицию и вращение (как в старом коде)
        UpdateCameraPosition(lookPoint);
        UpdateCameraRotation(); // ← ВАЖНО!
        
        yield return null;
    }
    
    currentHorizontalAngle = targetHorizontalAngle;
    UpdateCameraPosition(lookPoint);
    UpdateCameraRotation();
    
    // Обновляем скролл-оффсет чтобы камера продолжала смотреть на ту же точку
    if (useFixedPoint)
    {
        scrollOffset = lookPoint - groundLookPoint;
    }
    else if (lookAtTarget != null)
    {
        scrollOffset = lookPoint - lookAtTarget.position;
    }
    
    // Ограничиваем границы
    scrollOffset.x = Mathf.Clamp(scrollOffset.x, -scrollBounds.x, scrollBounds.x);
    scrollOffset.z = Mathf.Clamp(scrollOffset.z, -scrollBounds.y, scrollBounds.y);
    
    // Обновляем адаптивную точку
    if (useAdaptiveRotationPivot)
    {
        adaptiveRotationPivot = GetCurrentLookPoint();
        targetAdaptivePivot = adaptiveRotationPivot;
    }
    
    isRotating = false;
}
    
    private Vector3 GetCurrentLookPoint()
    {
        Vector3 basePoint = useFixedPoint ? groundLookPoint : 
                          (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
        
        // Добавляем смещение от скролла
        return basePoint + scrollOffset;
    }
    
   private void UpdateCameraPosition(Vector3 center)
{
    float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
    
    // Вычисляем горизонтальную позицию
    // МЕНЯЕМ ЗНАКИ: минус вместо плюса
    float xPos = center.x - Mathf.Sin(horizontalRad) * orbitRadius;
    float zPos = center.z - Mathf.Cos(horizontalRad) * orbitRadius;
    
    // Вычисляем вертикальную позицию с учетом угла наклона
    float horizontalDistance = orbitRadius;
    float yPos = center.y + Mathf.Tan(currentVerticalAngle * Mathf.Deg2Rad) * horizontalDistance;
    
    transform.position = new Vector3(xPos, yPos, zPos);
}
    
    private void UpdateCameraPosition()
    {
        UpdateCameraPosition(GetCurrentLookPoint());
    }

    public Vector3 GetLookAtPoint()
    {
        return GetCurrentLookPoint();
    }
    
    private void UpdateCameraRotation()
    {
        Vector3 lookPoint = GetCurrentLookPoint();
        
        // Вычисляем направление к точки
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
    
    public void SetZoomImmediate(float newRadius)
    {
        orbitRadius = Mathf.Clamp(newRadius, minOrbitRadius, maxOrbitRadius);
        targetOrbitRadius = orbitRadius;
    }
    
    public void ResetScroll()
    {
        scrollOffset = Vector3.zero;
    }
    
    public void SetScrollBounds(Vector2 newBounds)
    {
        scrollBounds = newBounds;
    }
    
    public void ResetAdaptivePivot()
    {
        adaptiveRotationPivot = GetCurrentLookPoint();
        targetAdaptivePivot = adaptiveRotationPivot;
        isAdaptingPivot = false;
    }
    
    private bool IsInGameMode() { return true; }
    
    // Метод для проверки текущих углов
    public void DebugCameraAngles()
    {
        Vector3 euler = transform.eulerAngles;
        float vertical = euler.x;
        if (vertical > 180f) vertical -= 360f;
        
        Debug.Log($"Камера: Горизонт={currentHorizontalAngle:F1}°, Вертикаль={vertical:F1}°, Радиус={orbitRadius:F1}");
        Debug.Log($"Скролл={scrollOffset}");
        if (useAdaptiveRotationPivot)
        {
            Debug.Log($"Адаптивная точка: {adaptiveRotationPivot}, Цель: {targetAdaptivePivot}");
        }
    }
    
    // Рисуем гизмо для визуализации границ
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        Vector3 center = useFixedPoint ? groundLookPoint : 
                        (lookAtTarget != null ? lookAtTarget.position : Vector3.zero);
        
        // Границы скролла
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center + new Vector3(0, 1, 0), 
                           new Vector3(scrollBounds.x * 2, 2, scrollBounds.y * 2));
        
        // Текущая точка взгляда
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetCurrentLookPoint(), 1f);
        
        // Адаптивная точка вращения
        if (useAdaptiveRotationPivot)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(adaptiveRotationPivot, 1.2f);
            Gizmos.DrawWireSphere(targetAdaptivePivot, 1.5f);
        }
        
        // Линия от камеры к точке
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, GetCurrentLookPoint());
    }
}