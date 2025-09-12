using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class DickControlledCube : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public float rotationSpeed = 10f;
    public LayerMask obstacleMask;
    public float checkDistance = 1f;

    [Header("Ground Settings")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.5f;
    public float cubeSize = 1f;

    [Header("Direction Tile Settings")]
    public string directionTileTag = "DirectionTile";
    public float tileActivationDelay = 0.3f;
    public float tileSize = 1f; 

    [Header("Visual Settings")]
    public float snapThreshold = 0.5f;
    public Color tileHighlightColor = Color.cyan;
    public float highlightDuration = 0.5f;
     // Добавляем новые настройки цвета
    [Header("Collision Settings")]
    public Color collisionColor = Color.red;
    public float colorResetDelay = 0.5f;
    public LayerMask collisionLayers;
     [Header("Level Completion")]
    public LayerMask levelCompleteLayer; // Слой для триггера завершения уровня
    public GameObject levelCompleteUI; // Ссылка на UI окно завершения уровня
     public float levelCompleteDelay = 1f; // Задержка перед показом UI
    public float triggerCenterThreshold = 0.5f; // Порог центра клетки (0.5 = середина)


    [Header("References")]
    public Transform mainPointer;
    public Transform visualPointer;

    [Header("Movement Control")]
    public bool movementEnabled = true;
    public Vector3 currentDirection = Vector3.forward;
    [Header("Jump Settings")]
public float jumpHeight = 1f;
public float jumpDistance = 2f;
public float jumpDuration = 0.8f;
public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1f), new Keyframe(1, 0));

private bool isJumping = false;
private Vector3 jumpStartPosition;
private Vector3 jumpTargetPosition;
[Header("Fragile Tile Settings")]
public string fragileTileTag = "FragileTile";
[Header("Speed Tile Settings")]
public string speedTileTag = "SpeedTile";
public Color speedTileHighlightColor = Color.yellow;
public float speedMultiplier = 2f;
public float speedBoostDuration = 3f;
private bool isSpeedBoosted = false; // ← ПЕРЕМЕЩАЕМ СЮДА!

private float originalSpeed;
public bool IsSpeedBoosted => isSpeedBoosted;
private Coroutine speedBoostCoroutine;

[HideInInspector]
public Vector3 InitialDirection;

    [SerializeField] private bool isRotating = false;
    [SerializeField] private bool isGrounded = true;
    private Vector3 lastGridPosition;
    private GameObject lastHighlightedTile;
    private Color originalTileColor;
    private Quaternion initialRotation;
    private Vector3 visualPointerLocalPosition;
    private Quaternion visualPointerLocalRotation;
    private GameObject lastDirectionTile;
    private Vector3 entryPoint;
    private float halfTileSize;
    private Vector3 tileEntryPoint;
    private bool isOnDirectionTile;
    public bool IsGrounded => isGrounded;
    public Vector3 InitialPosition;
public Rigidbody RB;
public bool startEnabled = true;
public bool IsMovementEnabled => movementEnabled;
 private MaterialPropertyBlock materialBlock;
    private Color originalColor;
    private bool isColliding;
     private GameObject currentFinishTrigger; // Текущий триггер финиша
    private Vector3 triggerEntryPoint; // Точка входа в триггер
    private GridObjectMover editModeChecker;
    [Header("Jump Tile Settings")]
public string jumpTileTag = "JumpTile"; // Тэг для тайлов прыжка
public Color jumpTileHighlightColor = Color.green; // Цвет подсветки для тайла прыжка

private GameObject lastJumpTile;
private bool isOnJumpTile;
private Vector3 jumpTileEntryPoint;
private Dictionary<GameObject, Color> tileOriginalColors = new Dictionary<GameObject, Color>();


    void Awake()
    {
        if(mainPointer != null) 
            currentDirection = mainPointer.forward;
    }

    void Start()
    {
    RB = GetComponent<Rigidbody>();
    InitialPosition = transform.position;
    // Добавьте эту строку
    InitialDirection = transform.forward;
    movementEnabled = startEnabled; 
        editModeChecker = FindAnyObjectByType<GridObjectMover>();
         if (mainPointer != null)
    {
        currentDirection = mainPointer.forward;
        InitialDirection = currentDirection; // ← СИНХРОНИЗАЦИЯ!
    }
    else
    {
        currentDirection = Vector3.forward;
        InitialDirection = currentDirection; // ← СИНХРОНИЗАЦИЯ!
    }

        RB = GetComponent<Rigidbody>();
        RB.freezeRotation = true;
        RB.useGravity = false;

        InitialPosition = transform.position;
        initialRotation = transform.rotation;

        if (visualPointer != null && mainPointer != null)
        {
            visualPointerLocalPosition = mainPointer.InverseTransformPoint(visualPointer.position);
            visualPointerLocalRotation = Quaternion.Inverse(mainPointer.rotation) * visualPointer.rotation;
        }

        
        lastGridPosition = GetSnappedPosition(transform.position);
        isGrounded = CheckGround();
        halfTileSize = tileSize / 2f;
        Debug.Log($"Rigidbody: isKinematic={RB.isKinematic}, UseGravity={RB.useGravity}, Drag={RB.linearDamping}");

         materialBlock = new MaterialPropertyBlock();
        GetComponent<MeshRenderer>().GetPropertyBlock(materialBlock);
        originalColor = GetComponent<MeshRenderer>().material.color;

         originalSpeed = speed; // ← ДОБАВЛЯЕМ В Start()
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(RotateToDirection(Vector3.forward));
        }
    }
    public float GetCurrentSpeed()
{
    return speed;
}
public float GetBaseSpeed()
{
    return originalSpeed;
}


void CheckSpeedTileUnderneath()
{
    if (string.IsNullOrEmpty(speedTileTag)) return;
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
    {
        if (hit.collider.CompareTag(speedTileTag))
        {
            HighlightTile(hit.collider.gameObject, speedTileHighlightColor);
        }
    }
}

// ДОБАВЛЯЕМ МЕТОД ДЛЯ АКТИВАЦИИ СКОРОСТИ
public void ActivateSpeedBoost()
{
    if (speedBoostCoroutine != null)
        StopCoroutine(speedBoostCoroutine);
    
    speedBoostCoroutine = StartCoroutine(SpeedBoostRoutine());
}
public void ExecuteBotMove(Vector3 targetPosition, Vector3 targetDirection)
{
    // Для бота - только телепортация и обновление направления
    transform.position = targetPosition;
    currentDirection = targetDirection;
    
    // Обновляем поинтеры без изменения вращения куба
    if (mainPointer != null)
    {
        mainPointer.forward = currentDirection;
        // Сохраняем локальные offset'ы чтобы не сломать визуал
        visualPointer.position = mainPointer.TransformPoint(visualPointerLocalPosition);
        visualPointer.rotation = mainPointer.rotation * visualPointerLocalRotation;
    }
    
    Debug.Log($"Bot teleported to {targetPosition}, direction: {currentDirection}");
}

// ДОБАВЛЯЕМ КОРУТИНУ СКОРОСТИ
private IEnumerator SpeedBoostRoutine()
{
    isSpeedBoosted = true;
    speed = originalSpeed * speedMultiplier;
    Debug.Log($"Скорость x{speedMultiplier}! Вжух!");
    
    // Визуальный эффект
    SetColor(Color.yellow);
    
    yield return new WaitForSeconds(speedBoostDuration);
    
    // Проверяем что объект еще активен
    if (this != null && gameObject.activeInHierarchy)
    {
        speed = originalSpeed;
        isSpeedBoosted = false;
        SetColor(originalColor);
        Debug.Log("Скорость вернулась к нормальной");
    }
}

public void ResetAllFragileTiles()
{
    FragileTile[] allFragileTiles = FindObjectsOfType<FragileTile>();
    foreach (FragileTile tile in allFragileTiles)
    {
        tile.ForceRespawn();
    }
    Debug.Log("Все хрупкие тайлы восстановлены");
}


public void PerformJump()
{
    if (isJumping || isRotating || !isGrounded) return;
    
    StartCoroutine(JumpRoutine());
}
private IEnumerator JumpRoutine()
{
    // Сохраняем состояние и отключаем стандартное управление
    isJumping = true;
    bool wasMovementEnabled = movementEnabled;
    movementEnabled = false;
    
    // Останавливаем физику
    RB.linearVelocity = Vector3.zero;
    RB.angularVelocity = Vector3.zero;
    
    // Запоминаем начальную позицию и рассчитываем целевую
    jumpStartPosition = transform.position;
    jumpTargetPosition = jumpStartPosition + currentDirection * jumpDistance;
    jumpTargetPosition = GetSnappedPosition(jumpTargetPosition); // Снэпим цель к сетке
    
    // Временно отключаем проверку земли чтобы избежать падения
    bool wasGravityEnabled = RB.useGravity;
    RB.useGravity = false;
    bool wasFreezeRotation = RB.freezeRotation;
    
    float elapsed = 0f;
    
    while (elapsed < jumpDuration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / jumpDuration;
        float curveValue = jumpCurve.Evaluate(progress);
        
        // Параболическая траектория: движение вперед + вертикальный подъем/спуск
        Vector3 newPosition = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, progress);
        newPosition.y = jumpStartPosition.y + curveValue * jumpHeight;
        
        // Плавное перемещение
        RB.MovePosition(newPosition);
        
        yield return null;
    }
    
    // Гарантированно становимся в конечную позицию
    Vector3 finalPosition = GetSnappedPosition(jumpTargetPosition);
    finalPosition.y = jumpStartPosition.y; // Возвращаем исходную высоту
    RB.MovePosition(finalPosition);
    
    // Восстанавливаем физические настройки
    RB.useGravity = wasGravityEnabled;
    RB.freezeRotation = wasFreezeRotation;
    CheckImmediateTileActivation(); // ← ВАЖНО!
     yield return new WaitForSeconds(0.1f); // 100ms должно хватить
    yield return new WaitForFixedUpdate();
    
    // Восстанавливаем состояние
    movementEnabled = wasMovementEnabled;
    isJumping = false;
    
    // Принудительно проверяем землю после приземления
    isGrounded = CheckGround();
    
    Debug.Log("Jump completed! Grounded: " + isGrounded);
}
private void CheckImmediateTileActivation()
{
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.5f))
    {
        if (hit.collider.CompareTag(directionTileTag) && !isRotating)
        {
            // Немедленно обрабатываем поворотный тайл
            Vector3 tileDirection = hit.collider.transform.forward;
            if (Vector3.Angle(currentDirection, tileDirection) > 5f)
            {
                StartCoroutine(RotateToDirection(tileDirection));
                isOnDirectionTile = false;
            }
        }
        else if (hit.collider.CompareTag(jumpTileTag) && !isJumping)
        {
            // Немедленно прыгаем с прыжкового тайла
            PerformJump();
            isOnJumpTile = false;
        }
        else if (hit.collider.CompareTag(speedTileTag))
        {
            // Немедленно активируем скорость
            ActivateSpeedBoost();
        }
    }
}


public void SetRotatingState(bool state) {
    isRotating = state;
    Debug.Log($"Rotation state set to: {state}");
}

    void FixedUpdate()
{
    UpdateVisualPointers();
    PeriodicGroundCheck();
    
    // ← ВАЖНО: Полностью отключаем ВСЮ логику тайлов в режиме редактирования
    bool shouldProcessTiles = editModeChecker == null || !editModeChecker.isInEditMode;
    
    
    if (shouldProcessTiles)
    {
        // Вся логика тайлов ТОЛЬКО в игровом режиме
       CheckDirectionTileUnderneath();
        CheckJumpTileUnderneath();
        CheckSpeedTileUnderneath();

        
        if (isOnDirectionTile && !isRotating && lastDirectionTile != null) 
        {
            float distance = Vector3.Dot(transform.position - tileEntryPoint, currentDirection);
            if (distance >= tileSize * 0.5f)
            {
                Vector3 tileDirection = lastDirectionTile.transform.forward;
                if (Vector3.Angle(currentDirection, tileDirection) > 5f)
                {
                    StartCoroutine(RotateToDirection(tileDirection));
                    isOnDirectionTile = false;
                }
            }
        }
        if (isOnJumpTile && !isJumping && !isRotating && lastJumpTile != null)
        {
            float distance = Vector3.Dot(transform.position - jumpTileEntryPoint, currentDirection);
            if (distance >= tileSize * 0.5f)
            {
                PerformJump();
                isOnJumpTile = false;
            }
        }
        
    }
    if (movementEnabled && !isRotating && !isJumping) // Добавляем проверку !isJumping
    {
        if (isGrounded)
        {
            HandleMovement();
        }
    }
}


void CheckJumpTileUnderneath()
{
    if (string.IsNullOrEmpty(jumpTileTag)) return;
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
    {
        if (hit.collider.CompareTag(jumpTileTag))
        {
            HighlightJumpTile(hit.collider.gameObject);
        }
    }
}

// Метод подсветки тайла прыжка
void HighlightJumpTile(GameObject tile)
{
    // Сбрасываем предыдущую подсветку
    if (lastHighlightedTile != null && lastHighlightedTile != tile)
    {
        ResetTileColor(lastHighlightedTile);
    }

    Renderer tileRenderer = tile.GetComponent<Renderer>();
    if (tileRenderer != null)
    {
        // Сохраняем оригинальный цвет если еще не сохранили
        if (!tileOriginalColors.ContainsKey(tile))
        {
            tileOriginalColors[tile] = tileRenderer.material.color;
        }
        
        tileRenderer.material.color = jumpTileHighlightColor;
        lastHighlightedTile = tile;
        Invoke(nameof(ResetLastTileColor), highlightDuration);
    }
}

 void OnTriggerEnter(Collider other)
{
    if (editModeChecker != null && editModeChecker.isInEditMode) return;
    
    if (((1 << other.gameObject.layer) & levelCompleteLayer) != 0)
    {
        currentFinishTrigger = other.gameObject;
        triggerEntryPoint = transform.position;
        return;
    }
    
    if (!string.IsNullOrEmpty(directionTileTag) && other.CompareTag(directionTileTag))
    {
        lastDirectionTile = other.gameObject;
        tileEntryPoint = transform.position;
        isOnDirectionTile = true;
    }
    
    // Добавляем обработку тайлов прыжка
    if (!string.IsNullOrEmpty(jumpTileTag) && other.CompareTag(jumpTileTag))
    {
        lastJumpTile = other.gameObject;
        jumpTileEntryPoint = transform.position;
        isOnJumpTile = true;
    }
     if (!string.IsNullOrEmpty(fragileTileTag) && other.CompareTag(fragileTileTag))
    {
        // Логика теперь в самом тайле, можно оставить пустое
    }
    if (!string.IsNullOrEmpty(speedTileTag) && other.CompareTag(speedTileTag))
    {
        ActivateSpeedBoost();
    }
}

 IEnumerator CompleteLevelWithDelay(GameObject finishTrigger)
    {
        // Останавливаем куб
        DisableMovement();
        
        // Уничтожаем флажок (триггер)
        Destroy(finishTrigger);
        currentFinishTrigger = null;
        
        // Ждем указанное время
        yield return new WaitForSeconds(levelCompleteDelay);
        
        // Показываем UI
        if (levelCompleteUI != null)
        {
            levelCompleteUI.SetActive(true);
        }
        
        Debug.Log("Level completed! Flag collected.");
    }

     void OnTriggerStay(Collider other)
    {
        if (editModeChecker != null && editModeChecker.isInEditMode) return;
       if (currentFinishTrigger != null && other.gameObject == currentFinishTrigger)
        {
            // Проверяем, достигли ли центра триггера
            if (HasReachedTriggerCenter(other))
            {
                StartCoroutine(CompleteLevelWithDelay(other.gameObject));
            }
        }
    }
    void OnTriggerExit(Collider other)
{
    if (editModeChecker != null && editModeChecker.isInEditMode) return;
    
    if (other.CompareTag(directionTileTag))
    {
        isOnDirectionTile = false;
        lastDirectionTile = null;
    }
    
    // Добавляем обработку выхода с тайла прыжка
    if (other.CompareTag(jumpTileTag))
    {
        isOnJumpTile = false;
        lastJumpTile = null;
    }
    
    if (other.gameObject == currentFinishTrigger)
    {
        currentFinishTrigger = null;
    }
}

    bool HasReachedTriggerCenter(Collider trigger)
    {
        // Рассчитываем относительную позицию куба в триггере
        Vector3 localPos = trigger.transform.InverseTransformPoint(transform.position);
        
        // Проверяем по всем осям (можно настроить отдельно для X и Z)
        return Mathf.Abs(localPos.x) <= triggerCenterThreshold && 
               Mathf.Abs(localPos.z) <= triggerCenterThreshold;
    }


 void UpdateVisualPointers()
    {
        if (visualPointer == null || mainPointer == null) return;
        
        visualPointer.position = mainPointer.TransformPoint(visualPointerLocalPosition);
        visualPointer.rotation = mainPointer.rotation * visualPointerLocalRotation;
    }


    public void ToggleMovement()
    {
        movementEnabled = !movementEnabled;
        Debug.Log($"Movement {(movementEnabled ? "ENABLED" : "DISABLED")}");
    }

    public void ResetSpeedBoost()
{
    if (speedBoostCoroutine != null)
    {
        StopCoroutine(speedBoostCoroutine);
        speedBoostCoroutine = null;
    }
    
    isSpeedBoosted = false;
    speed = originalSpeed;
    SetColor(originalColor);
    Debug.Log("Speed boost reset");
}
    
  public void DisableMovement()
{
    movementEnabled = false;
    
    if (TryGetComponent<Rigidbody>(out var RB))
    {
        // Для kinematic bodies только останавливаем вращение
        if (!RB.isKinematic)
        {
            RB.linearVelocity = Vector3.zero;
        }
        RB.angularVelocity = Vector3.zero;
    }
}
public void ResetPhysics()
{
    if (TryGetComponent<Rigidbody>(out var RB))
    {
        if (!RB.isKinematic)
        {
            RB.linearVelocity = Vector3.zero;
        }
        RB.angularVelocity = Vector3.zero;
        RB.freezeRotation = true;
    }
    isGrounded = CheckGround();
}

   public void UpdateDirection(Vector3 newDirection)
{
    currentDirection = newDirection;
    if(mainPointer != null) mainPointer.forward = newDirection;
    if(visualPointer != null) visualPointer.forward = newDirection;
    
    Debug.Log($"Direction updated to: {newDirection}");
}
public void Revive()
{
    if (TryGetComponent<Rigidbody>(out var RB))
    {
        RB.WakeUp();
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
    }
    
    // Сброс цветового эффекта
    if (TryGetComponent<CollisionColorChanger>(out var colorChanger))
    {
        colorChanger.ResetCollisionEffect();
    }
    
    isGrounded = CheckGround();
}

public void StopGame()
{
    // Останавливаем движение
    DisableMovement();
    
    // Сбрасываем физику
    ResetPhysics();
    
    // Сбрасываем цвета тайлов
    ResetAllTileColors();
     // Сбрасываем ускорение ← ДОБАВИТЬ ЭТО
    ResetSpeedBoost();
    // Восстанавливаем хрупкие тайлы ← ДОБАВИТЬ ЭТО
    ResetAllFragileTiles();
    
    Debug.Log("Игра остановлена");
}

public void ResetAllTileColors()
{
    CancelInvoke(nameof(ResetLastTileColor));
    
    // Сбрасываем все тайлы, цвета которых мы сохранили
    foreach (var tileEntry in tileOriginalColors)
    {
        if (tileEntry.Key != null)
        {
            Renderer renderer = tileEntry.Key.GetComponent<Renderer>();
            if (renderer != null) 
            {
                renderer.material.color = tileEntry.Value;
            }
        }
    }
    
    lastHighlightedTile = null;
    Debug.Log("Все тайлы сброшены");
}

public void OnStopGameClick()
{
    DickControlledCube cube = FindObjectOfType<DickControlledCube>();
    if (cube != null)
    {
        cube.StopGame();
    }
}

public void FullReset() {
    StopAllCoroutines();
    isRotating = false;
    isGrounded = true;
    movementEnabled = false;
     // Сбрасываем ускорение ← ДОБАВИТЬ ЭТО
    ResetSpeedBoost();
    // Восстанавливаем хрупкие тайлы ← ДОБАВИТЬ ЭТО
    ResetAllFragileTiles();
    
    if (TryGetComponent<Rigidbody>(out var RB)) {
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        RB.isKinematic = false;
        RB.freezeRotation = true;
    }
    
    // Принудительная проверка земли
    StartCoroutine(DelayedGroundCheck());
}

private IEnumerator DelayedGroundCheck() {
    yield return new WaitForFixedUpdate();
    isGrounded = CheckGround();
    Debug.Log($"Ground check after reset: {isGrounded}");
}
public void ForceUpdateDirection(Vector3 newDirection)
{
    currentDirection = newDirection.normalized;
    mainPointer.forward = currentDirection; // Жёстко синхронизируем
    visualPointer.forward = currentDirection;
    
    Debug.Log($"Direction updated: {currentDirection}");
}

    public void ResetToInitialState()
    {
        transform.position = InitialPosition;
        transform.rotation = initialRotation;
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        currentDirection = mainPointer.forward;
        isRotating = false;
        isGrounded = true;
        RB.freezeRotation = true;
        RB.useGravity = false;
         // Сбрасываем ускорение ← ДОБАВИТЬ ЭТО
    ResetSpeedBoost();
     // Восстанавливаем хрупкие тайлы ← ДОБАВИТЬ ЭТО
    ResetAllFragileTiles();
        
        // Сброс визуального указателя
        if (visualPointer != null && mainPointer != null)
        {
            visualPointer.position = mainPointer.TransformPoint(visualPointerLocalPosition);
            visualPointer.rotation = mainPointer.rotation * visualPointerLocalRotation;
        }
    }

    void HandleMovement()
    {
        if (isJumping) return; // Не двигаемся во время прыжка
        if (ShouldSnapToGrid())
        {
            SnapToGrid();
        }

        bool hasObstacle = Physics.Raycast(
            transform.position, 
            currentDirection, 
            checkDistance, 
            collisionLayers);

        if (hasObstacle)
        {
            if (!isColliding)
            {
                StartCollision();
            }
            RB.linearVelocity = Vector3.zero;
        }
        else
        {
            if (isColliding)
            {
                EndCollision();
            }
            RB.linearVelocity = currentDirection * speed;
        }
    }

     void StartCollision()
    {
        isColliding = true;
        SetColor(collisionColor);
        DisableMovement();
        // Если нужно автоматическое восстановление цвета
        if (colorResetDelay > 0)
        {
            Invoke("EndCollision", colorResetDelay);
        }
    }

    void EndCollision()
    {
        if (!isColliding) return;
        
        isColliding = false;
        SetColor(originalColor);
    }

    void SetColor(Color color)
{
    var renderer = GetComponent<MeshRenderer>();
    renderer.GetPropertyBlock(materialBlock);
    materialBlock.SetColor("_BaseColor", color); // Для URP/HDRP
    materialBlock.SetColor("_Color", color);    // Для стандартного шейдера
    renderer.SetPropertyBlock(materialBlock);
    
    Debug.Log($"Color set to: {color}"); // Добавьте этот лог
}

    void OnDisable()
    {
        CancelInvoke(); // Отменяем все запланированные вызовы
        EndCollision(); // Восстанавливаем цвет
        // Сбрасываем ускорение при выключении ← ДОБАВИТЬ ЭТО
    if (isSpeedBoosted)
    {
        ResetSpeedBoost();
    }
    }

// Новый вспомогательный метод для проверки тайлов
private bool CheckDirectionTileUnderneath(out Vector3 tileDirection)
{
    tileDirection = Vector3.zero;
     // Не подсвечиваем тайлы в режиме редактирования
    if (editModeChecker != null && editModeChecker.isInEditMode) return false;
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
    {
        if (hit.collider.CompareTag(directionTileTag))
        {
            tileDirection = hit.transform.forward;
            return true;
        }
    }
    return false;
}

// Проверка необходимости поворота на тайле
private bool ShouldRotateOnTile(Vector3 tileDirection)
{
    return Vector3.Angle(currentDirection, tileDirection) > 5f && HasPassedHalfCell();
}
bool ShouldSnapToGrid()
{
    return Vector3.Distance(transform.position, GetSnappedPosition(transform.position)) > 0.05f;
}

  IEnumerator RotateToDirection(Vector3 newDirection)
    {
        if (isRotating) yield break;
        
        isRotating = true;
        RB.linearVelocity = Vector3.zero;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(newDirection);
        float elapsed = 0f;

        while (elapsed < 1f)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed);
            mainPointer.rotation = transform.rotation;
            currentDirection = transform.forward;
            elapsed += Time.fixedDeltaTime * rotationSpeed;
            yield return new WaitForFixedUpdate();
        }

        transform.rotation = targetRotation;
        mainPointer.rotation = targetRotation;
        currentDirection = newDirection;
        SnapToGrid();
        isRotating = false;
    }

    IEnumerator RotateOnCollision()
    {
        isRotating = true;
        RB.linearVelocity = Vector3.zero;

        RaycastHit hit;
        Physics.Raycast(transform.position, currentDirection, out hit, checkDistance, obstacleMask);
        Vector3 newDirection = Vector3.Reflect(currentDirection, hit.normal).normalized;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(newDirection);
        float elapsed = 0f;

        while (elapsed < 1f)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed);
            mainPointer.rotation = transform.rotation;
            currentDirection = transform.forward;
            elapsed += Time.fixedDeltaTime * rotationSpeed;
            yield return new WaitForFixedUpdate();
        }

        transform.rotation = targetRotation;
        mainPointer.rotation = targetRotation;
        currentDirection = newDirection;
        SnapToGrid();
        isRotating = false;
    }


    void CheckDirectionTileUnderneath()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
        {
            if (hit.collider.CompareTag(directionTileTag))
            {
                HighlightTile(hit.collider.gameObject, tileHighlightColor);
            }
        }
    }

   void HighlightTile(GameObject tile, Color highlightColor)
{
    if (lastHighlightedTile != null && lastHighlightedTile != tile)
    {
        ResetTileColor(lastHighlightedTile);
    }

    Renderer tileRenderer = tile.GetComponent<Renderer>();
    if (tileRenderer != null)
    {
        // Сохраняем оригинальный цвет если еще не сохранили
        if (!tileOriginalColors.ContainsKey(tile))
        {
            tileOriginalColors[tile] = tileRenderer.material.color;
        }
        
        tileRenderer.material.color = highlightColor;
        lastHighlightedTile = tile;
        Invoke(nameof(ResetLastTileColor), highlightDuration);
    }
}

    void ResetTileColor(GameObject tile)
{
    if (tile != null && tileOriginalColors.ContainsKey(tile))
    {
        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer != null) 
        {
            renderer.material.color = tileOriginalColors[tile];
            
            // Опционально: можно удалить из словаря после сброса
            // tileOriginalColors.Remove(tile);
        }
    }
}

   void ResetLastTileColor()
{
    if (lastHighlightedTile != null)
    {
        ResetTileColor(lastHighlightedTile);
        lastHighlightedTile = null;
    }
}

    void PeriodicGroundCheck()
    {
        if (isJumping) return; // Не проверяем землю во время прыжка
        isGrounded = CheckGround();
        if (!isGrounded) StartFalling();
    }

    bool CheckGround() {
    Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.red, 0.5f);
    bool grounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
    Debug.Log($"Ground check: {grounded}");
    return grounded;
}

    void StartFalling()
    {
        RB.freezeRotation = false;
        RB.useGravity = true;
    }

  void SnapToGrid() {
    // Снэп только при почти нулевой скорости
    if (RB.linearVelocity.magnitude < 0.1f) {
        Vector3 snappedPos = GetSnappedPosition(transform.position);
        snappedPos.y = transform.position.y;
        RB.MovePosition(snappedPos); // Плавное перемещение
    }
}

   Vector3 GetSnappedPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / tileSize) * tileSize,
            position.y,
            Mathf.Round(position.z / tileSize) * tileSize
        );
    }

    bool HasPassedHalfCell()
    {
        Vector3 snappedPos = GetSnappedPosition(transform.position);
        return Vector3.Distance(snappedPos, lastGridPosition) >= halfTileSize;
    }

    // Этот метод можно привязать к UI кнопке "Force Ground"
public void ForceGroundedState()
{
    // Устанавливаем флаг в true
    isGrounded = true;
    
    // Дополнительные действия для корректного состояния:
    RB.freezeRotation = true;
    RB.useGravity = false;
    
    // Принудительно снэпаем к сетке, если нужно
    SnapToGrid();
    
    Debug.Log("Forced grounded state: TRUE");
}
}