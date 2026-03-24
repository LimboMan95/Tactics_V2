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

    [Header("Physics (smooth constant speed)")]
    [Tooltip("Rigidbody damping съедает скорость между FixedUpdate — даёт рывки по клеткам. Для аркады обычно 0.")]
    [SerializeField] private float rigidbodyLinearDamping = 0f;
    [SerializeField] private float rigidbodyAngularDamping = 0f;
    [Tooltip("Трение с полом тормозит тело даже при выставлении velocity каждый кадр.")]
    [SerializeField] private bool applyZeroFrictionColliderMaterial = true;

    [Header("Ground Settings")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.5f;
    public float cubeSize = 1f;

    [Header("Fall death")]
    [Tooltip("Если выключено — падение за карту не убивает (только бомба и т.п.).")]
    [SerializeField] private bool enableFallDeath = true;
    [Tooltip("Смерть, если Y ниже этой мировой координаты (подстрой под пол уровня).")]
    [SerializeField] private float fallDeathWorldY = -12f;
    [Tooltip("Дополнительно: смерть, если ниже точки старта куба больше чем на столько метров.")]
    [SerializeField] private float maxFallBelowSpawnY = 30f;

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
[Tooltip("Высота вершины дуги в мировых единицах (1 клетка при tileSize=1 → 1).")]
public float jumpHeight = 1f;

    [Header("Explosion Settings")]
    [Tooltip("Префаб для фрагментов куба при взрыве")]
    public GameObject cubeFragmentPrefab;
    [Tooltip("Количество фрагментов при взрыве (8-11)")]
    [Range(8, 11)] public int fragmentCount = 9;
    [Tooltip("Сила взрыва, разбрасывающая фрагменты")]
    public float explosionForce = 10f;
    [Tooltip("Разброс силы взрыва по осям")]
    public Vector3 explosionRandomness = new Vector3(2f, 3f, 2f);
    [Tooltip("Размер фрагментов относительно оригинального куба")]
    public float fragmentSize = 0.4f;
    
    private List<GameObject> currentFragments = new List<GameObject>();
    private Vector3 originalScale;
    private bool isExploded = false;
[Tooltip("Дальность по горизонтали в мировых единицах (2 клетки при tileSize=1 → 2). Скорость vx считается из времени полёта.")]
public float jumpDistance = 2f;
[Tooltip("Длительность фазы уменьшенного коллайдера (сек). Не задаёт дальность — она из jumpDistance и jumpHeight.")]
public float jumpDuration = 0.8f;
public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1f), new Keyframe(1, 0));
public float speedBoostJumpMultiplier = 2f; // Во сколько раз длиннее прыжок при ускорении
public bool isJumping = false;
private Vector3 jumpStartPosition;
private Vector3 jumpTargetPosition;
// НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ДВУХФАЗНОГО ПРЫЖКА
[Header("Two-Phase Jump")]
public float phaseTwoStart = 0.7f; // 70% прыжка
public float smallColliderSize = 0.6f; // Размер коллайдера в первой фазе
public float normalColliderSize = 1f; // Нормальный размер
private Vector3 originalColliderSize;
private Vector3 originalColliderCenter;

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

    private BoxCollider _boxCollider;
    private const float SnapVelocityThreshold = 0.08f;
    private bool isDead;
    private Coroutine _jumpRoutine;

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
        RB.useGravity = true;
        RB.linearDamping = rigidbodyLinearDamping;
        RB.angularDamping = rigidbodyAngularDamping;
        RB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        InitialPosition = transform.position;
        initialRotation = transform.rotation;

        if (visualPointer != null && mainPointer != null)
        {
            visualPointerLocalPosition = mainPointer.InverseTransformPoint(visualPointer.position);
            visualPointerLocalRotation = Quaternion.Inverse(mainPointer.rotation) * visualPointer.rotation;
        }

        
        lastGridPosition = GetSnappedPosition(transform.position);
        _boxCollider = GetComponent<BoxCollider>();
        if (_boxCollider != null)
        {
            originalColliderSize = _boxCollider.size;
            originalColliderCenter = _boxCollider.center;
            if (applyZeroFrictionColliderMaterial)
            {
                var pm = new PhysicsMaterial("CubeMovement_NoFriction")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                _boxCollider.material = pm;
            }
        }
        isGrounded = CheckGround();
        halfTileSize = tileSize / 2f;

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
public void GameOver()
{
    if (isDead) return;
    isDead = true;
    Debug.Log("💥 КУБ ВЗОРВАН!");
    
    // Останавливаем движение оригинального куба
    DisableMovement();
    
    // Вместо просто остановки - взрываем куб на фрагменты
    ExplodeIntoFragments();
    
    // Скрываем оригинальный куб
    SetCubeVisible(false);
}

private void ExplodeIntoFragments()
{
    if (cubeFragmentPrefab == null)
    {
        Debug.LogWarning("Cube fragment prefab not assigned! Using simple cube.");
        // Создаем простые кубы если префаб не назначен
        CreateSimpleFragments();
        return;
    }
    
    ClearExistingFragments();
    isExploded = true;
    
    Vector3 explosionCenter = transform.position;
    
    for (int i = 0; i < fragmentCount; i++)
    {
        GameObject fragment = Instantiate(cubeFragmentPrefab, explosionCenter, Random.rotation);
        fragment.transform.localScale = Vector3.one * fragmentSize;
        
        // Добавляем физику
        Rigidbody rb = fragment.GetComponent<Rigidbody>();
        if (rb == null)
            rb = fragment.AddComponent<Rigidbody>();
            
        // Применяем случайную силу взрыва
        Vector3 randomForce = new Vector3(
            Random.Range(-explosionRandomness.x, explosionRandomness.x),
            Random.Range(explosionRandomness.y * 0.5f, explosionRandomness.y),
            Random.Range(-explosionRandomness.z, explosionRandomness.z)
        ) * explosionForce;
        
        rb.AddForce(randomForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);
        
        currentFragments.Add(fragment);
    }
}

private void CreateSimpleFragments()
{
    ClearExistingFragments();
    isExploded = true;
    
    Vector3 explosionCenter = transform.position;
    
    for (int i = 0; i < fragmentCount; i++)
    {
        GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fragment.transform.position = explosionCenter;
        fragment.transform.localScale = Vector3.one * fragmentSize;
        fragment.GetComponent<Renderer>().material.color = GetComponent<Renderer>().material.color;
        
        // Добавляем физику
        Rigidbody rb = fragment.AddComponent<Rigidbody>();
        
        // Применяем случайную силу взрыва
        Vector3 randomForce = new Vector3(
            Random.Range(-explosionRandomness.x, explosionRandomness.x),
            Random.Range(explosionRandomness.y * 0.5f, explosionRandomness.y),
            Random.Range(-explosionRandomness.z, explosionRandomness.z)
        ) * explosionForce;
        
        rb.AddForce(randomForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);
        
        currentFragments.Add(fragment);
    }
}

private void ClearExistingFragments()
{
    foreach (GameObject fragment in currentFragments)
    {
        if (fragment != null)
            Destroy(fragment);
    }
    currentFragments.Clear();
}

private void SetCubeVisible(bool visible)
{
    Renderer renderer = GetComponent<Renderer>();
    if (renderer != null)
        renderer.enabled = visible;
        
    Collider collider = GetComponent<Collider>();
    if (collider != null)
        collider.enabled = visible;
}

public void ReassembleCube()
{
    if (!isExploded) return;
    
    Debug.Log("🧩 Собираем куб обратно!");
    
    // Удаляем все фрагменты
    ClearExistingFragments();
    
    // Показываем оригинальный куб
    SetCubeVisible(true);
    
    // Сбрасываем состояние смерти
    isDead = false;
    isExploded = false;
    
    // Сбрасываем физику
    ResetPhysics();
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
    // Используем без сортировки - порядок не важен, только быстродействие
    FragileTile[] allFragileTiles = FindObjectsByType<FragileTile>(FindObjectsSortMode.None);
    
    foreach (FragileTile tile in allFragileTiles)
    {
        if (tile != null) tile.ForceRespawn();
    }
    Debug.Log($"Все хрупкие тайлы восстановлены ({allFragileTiles.Length} шт.)");
}

private void CheckAllImmediateActivations()
{
    CheckAllImmediateActivations();
    CheckAllImmediateActivations();
}
public void ResetAllResettableObjects()
{
    // Ищем активные объекты с интерфейсом
    MonoBehaviour[] allObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
    int resetCount = 0;
    
    foreach (MonoBehaviour obj in allObjects)
    {
        IResettable resettable = obj as IResettable;
        if (resettable != null)
        {
            Debug.Log($"Resetting (active): {obj.name}");
            resettable.ResetObject();
            resetCount++;
        }
    }
    
    // Ищем ВСЕ объекты с компонентом Bomb (включая неактивные!)
    Bomb[] bombs = Resources.FindObjectsOfTypeAll<Bomb>();
    foreach (Bomb bomb in bombs)
    {
        if (bomb.gameObject.scene.isLoaded) // Проверяем что в текущей сцене
        {
            Debug.Log($"🔥 Resetting bomb (inactive): {bomb.name}");
            bomb.ResetObject();
            resetCount++;
        }
    }
    
    Debug.Log($"✅ Сброшено {resetCount} объектов");
}

private bool CheckImmediateFlagActivation()
{
    // Проверяем коллайдеры вокруг куба
    Collider[] colliders = Physics.OverlapSphere(
        transform.position, 
        tileSize * 0.6f); // Немного больше чем половина тайла
    
    foreach (var collider in colliders)
    {
        if (((1 << collider.gameObject.layer) & levelCompleteLayer) != 0)
        {
            // Устанавливаем текущий триггер
            currentFinishTrigger = collider.gameObject;
            triggerEntryPoint = transform.position;
            
            // Проверяем достижение центра
            if (HasReachedTriggerCenter(collider))
            {
                StartCoroutine(CompleteLevelWithDelay(collider.gameObject));
                return true; // Флаг активирован
            }
            else
            {
                // Вошли в триггер, но не в центре
                Debug.Log("Landed on flag edge - will check in OnTriggerStay");
                return false;
            }
        }
    }
    
    return false; // Флаг не найден
}


public void PerformJump()
{
     Debug.Log($"🎯 PerformJump START from {Time.frameCount}");
    Debug.Log($"🎯 PerformJump called: isJumping={isJumping}, isRotating={isRotating}, isGrounded={isGrounded}");
    
    if (isJumping || isRotating || !isGrounded)
    {
        Debug.Log($"   ❌ Прыжок отклонён: isJumping={isJumping}, isRotating={isRotating}, isGrounded={isGrounded}");
        return;
    }
    
    _jumpRoutine = StartCoroutine(JumpRoutine());
}
private IEnumerator JumpRoutine()
{
    if (isJumping || isRotating || !isGrounded) yield break;
    
    isJumping = true;
    bool wasMovementEnabled = movementEnabled;
    movementEnabled = false;

    try
    {
    
    // Очищаем скорость
    RB.linearVelocity = Vector3.zero;
    RB.angularVelocity = Vector3.zero;
    
    // Расчет дистанции с учётом ускорения
    float currentJumpDistance = jumpDistance;
    if (isSpeedBoosted)
        currentJumpDistance *= speedBoostJumpMultiplier;
    
    Vector3 jumpDirection = currentDirection.normalized;
    
    float g = Mathf.Abs(Physics.gravity.y);
    if (g < 0.01f) g = 9.81f;

    float verticalSpeed = Mathf.Sqrt(2f * g * jumpHeight);
    float flightTime = 2f * verticalSpeed / g;
    if (flightTime < 0.02f) flightTime = 0.02f;

    float horizontalSpeed = currentJumpDistance / flightTime;

    RB.linearVelocity = jumpDirection * horizontalSpeed + Vector3.up * verticalSpeed;

    float colliderPhaseDuration = Mathf.Min(jumpDuration, flightTime * 0.98f);

    BoxCollider boxCol = GetComponent<BoxCollider>();
    float elapsed = 0f;

    while (elapsed < colliderPhaseDuration && isJumping)
    {
        elapsed += Time.deltaTime;
        float progress = colliderPhaseDuration > 1e-4f ? elapsed / colliderPhaseDuration : 1f;
        
        if (boxCol != null)
        {
            if (progress < phaseTwoStart)
            {
                boxCol.size = Vector3.one * smallColliderSize;
                boxCol.center = Vector3.zero;
            }
            else
            {
                boxCol.size = Vector3.one * normalColliderSize;
                boxCol.center = Vector3.zero;
            }
        }
        
        yield return null;
    }
    
    if (boxCol != null)
    {
        boxCol.size = originalColliderSize;
        boxCol.center = originalColliderCenter;
    }

    yield return new WaitUntil(() => !isGrounded);
    yield return new WaitUntil(() => isGrounded);
    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();
    
    // Снэпаем позицию
    Vector3 snappedPos = GetSnappedPosition(transform.position);
    snappedPos.y = transform.position.y;
    RB.MovePosition(snappedPos);
    
    // Проверяем поворотные тайлы
    CheckDirectionTileOnly();
    Debug.Log($"🪂 Приземление: pos={transform.position}");
    
    // Сбрасываем флаг прыжка
    isJumping = false;
    
    // Проверяем все тайлы
    CheckImmediateTileActivation();
    
    // Нормализуем скорость после прыжка
    if (isSpeedBoosted)
        speed = originalSpeed * speedMultiplier;
    else
        speed = originalSpeed;
    RB.linearVelocity = currentDirection * speed;
    
    movementEnabled = wasMovementEnabled;
    isGrounded = CheckGround();
    
    // Проверка на ящик
    Collider[] landingCrates = Physics.OverlapBox(
        transform.position,
        new Vector3(cubeSize * 0.4f, 0.3f, cubeSize * 0.4f),
        Quaternion.identity,
        LayerMask.GetMask("Crate")
    );
    
    if (landingCrates.Length > 0)
    {
        Debug.Log("📦 Приземлился на ящик!");
        if (!isColliding) StartCollision();
        RB.linearVelocity = Vector3.zero;
        movementEnabled = false;
    }
    
    Debug.Log($"Jump completed. Grounded: {isGrounded}");
    }
    finally
    {
        _jumpRoutine = null;
    }
}

// === НОВЫЙ МЕТОД: ТОЛЬКО ПОВОРОТНЫЕ ТАЙЛЫ ===
private void CheckDirectionTileOnly()
{
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.5f))
    {
        if (hit.collider.CompareTag(directionTileTag) && !isRotating)
        {
            Vector3 tileDirection = hit.collider.transform.forward;
            Debug.Log($"🔄 CheckDirectionTileOnly: тайл {hit.collider.name} | угол={Vector3.Angle(currentDirection, tileDirection)}");
            
            if (Vector3.Angle(currentDirection, tileDirection) > 5f)
            {
                Debug.Log($"   ✅ ПОВОРОТ ИЗ CHECKDIRECTIONTILEONLY");
                StartCoroutine(RotateToDirection(tileDirection));
                isOnDirectionTile = false;
            }
        }
    }
}
private void CheckImmediateTileActivation()
{
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.5f))
    {
        if (hit.collider.CompareTag(directionTileTag) && !isRotating)
{
    Vector3 tileDirection = hit.collider.transform.forward;
    Debug.Log($"🔄 CheckImmediate: поворотный тайл {hit.collider.name} | угол={Vector3.Angle(currentDirection, tileDirection)}");
    
    if (Vector3.Angle(currentDirection, tileDirection) > 5f)
    {
        Debug.Log($"   ✅ ПОВОРОТ ИЗ CHECKIMMEDIATE");
        StartCoroutine(RotateToDirection(tileDirection));
        isOnDirectionTile = false;
    }
}
        // ← ОСТАВЛЯЕМ прыжковые тайлы
        else if (hit.collider.CompareTag(jumpTileTag) && !isJumping && !isRotating)
        {
            // Проверяем находимся ли мы достаточно близко к центру тайла
            Vector3 tileCenter = hit.collider.transform.position;
            float distanceToCenter = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(tileCenter.x, 0, tileCenter.z));
            
            // Используем ЕДИНЫЙ порог triggerCenterThreshold
            if (distanceToCenter <= triggerCenterThreshold)
            {
                Debug.Log($"📏 CheckImmediate: прыжок на {hit.collider.name} | distance={distanceToCenter} | threshold={triggerCenterThreshold}");
                PerformJump();
                isOnJumpTile = false;
            }
            else
            {
                // Если не в центре - запоминаем для обработки в FixedUpdate
                lastJumpTile = hit.collider.gameObject;
                jumpTileEntryPoint = transform.position;
                isOnJumpTile = true;
            }
        }
        else if (hit.collider.CompareTag(speedTileTag))
        {
            ActivateSpeedBoost();
        }
    }
}

private void HandleImmediateFlagActivation(GameObject flag)
{
    // Устанавливаем текущий триггер флага
    currentFinishTrigger = flag;
    triggerEntryPoint = transform.position;
    
    // Немедленно проверяем достижение центра
    if (HasReachedTriggerCenter(flag.GetComponent<Collider>()))
    {
        StartCoroutine(CompleteLevelWithDelay(flag));
    }
    else
    {
        // Если не в центре, просто отмечаем что вошли в триггер
        // Дальнейшая проверка будет в OnTriggerStay
        Debug.Log("Landed on flag but not in center - waiting...");
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
    if (isOnJumpTile && !isJumping && !isRotating && lastJumpTile != null)
{
    float distance = Vector3.Dot(transform.position - jumpTileEntryPoint, currentDirection);
    
    if (distance >= tileSize * 0.5f)
    {
        PerformJump();
        isOnJumpTile = false;
    }
}
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

    CheckFallDeath();
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
    
    Debug.Log($"Trigger enter: {other.gameObject.name}");
    if (other.CompareTag(directionTileTag))
    {
        Debug.Log($"🎯 Вход в тайл {other.name} на позиции {other.transform.position}");
    }
    
    if (((1 << other.gameObject.layer) & levelCompleteLayer) != 0)
    {
        Debug.Log($"Flag entered: {other.gameObject.name}");
        currentFinishTrigger = other.gameObject;
        triggerEntryPoint = transform.position;
        
        // Немедленная проверка при входе
        if (HasReachedTriggerCenter(other))
        {
            Debug.Log("Flag center reached on enter!");
            StartCoroutine(CompleteLevelWithDelay(other.gameObject));
        }
        return;
    }
    
    if (!string.IsNullOrEmpty(directionTileTag) && other.CompareTag(directionTileTag))
    {
        lastDirectionTile = other.gameObject;
        tileEntryPoint = transform.position;
        isOnDirectionTile = true;
    }
    
    if (!string.IsNullOrEmpty(jumpTileTag) && other.CompareTag(jumpTileTag))
    {
        lastJumpTile = other.gameObject;
        jumpTileEntryPoint = transform.position;
        isOnJumpTile = true;
    }
    
    if (!string.IsNullOrEmpty(fragileTileTag) && other.CompareTag(fragileTileTag))
    {
        // логика в самом тайле
    }
    
    if (!string.IsNullOrEmpty(speedTileTag) && other.CompareTag(speedTileTag))
    {
        ActivateSpeedBoost();
    }
}

void OnTriggerStay(Collider other)
{
    if (editModeChecker != null && editModeChecker.isInEditMode) return;
    
    // ===== ПОВОРОТНЫЙ ТАЙЛ =====
    if (other.CompareTag(directionTileTag))
    {
        bool centerReached = HasReachedTriggerCenter(other);
        float angle = Vector3.Angle(currentDirection, other.transform.forward);
        Vector3 tileDirection = other.transform.forward;
        
        Debug.Log($"🔄=== ПОВОРОТ ===");
        Debug.Log($"   Тайл: {other.name}");
        Debug.Log($"   Позиция куба: {transform.position}");
        Debug.Log($"   Позиция тайла: {other.transform.position}");
        Debug.Log($"   localPos: {other.transform.InverseTransformPoint(transform.position)}");
        Debug.Log($"   isRotating: {isRotating}");
        Debug.Log($"   isGrounded: {isGrounded}");
        Debug.Log($"   centerReached: {centerReached}");
        Debug.Log($"   triggerCenterThreshold: {triggerCenterThreshold}");
        Debug.Log($"   Угол: {angle}");
        
        // ДОБАВЛЕНА ПРОВЕРКА, ЧТО УГОЛ РЕАЛЬНО БОЛЬШОЙ
        if (!isRotating && centerReached && angle > 5f)
        {
            Debug.Log($"   ✅ ПОВОРОТ ВЫПОЛНЯЕТСЯ! Угол={angle}");
            currentDirection = tileDirection;
            mainPointer.forward = tileDirection;
            visualPointer.forward = tileDirection;
            StartCoroutine(RotateToDirection(tileDirection));
        }
        else if (!isRotating && centerReached && angle <= 5f)
        {
            Debug.Log($"   ❌ Угол мал: {angle} ≤ 5 — поворот не нужен, isRotating НЕ ВКЛЮЧАЕТСЯ");
        }
        else
        {
            Debug.Log($"   ❌ Условия не выполнены: !isRotating={!isRotating}, centerReached={centerReached}, angle={angle}");
        }
    }
    
    // ===== ПРЫЖКОВЫЙ ТАЙЛ =====
    if (other.CompareTag(jumpTileTag))
    {
        bool centerReached = HasReachedTriggerCenter(other);
        Debug.Log($"🦘 Прыжковый тайл {other.name} | isJumping={isJumping} | isRotating={isRotating} | isGrounded={isGrounded} | centerReached={centerReached}");
        
        if (!isJumping && !isRotating && isGrounded && centerReached)
        {
            Debug.Log($"   ✅ ПРЫЖОК ВЫПОЛНЯЕТСЯ!");
            PerformJump();
        }
    }
    
    // ===== ФЛАГ =====
    if (currentFinishTrigger != null && other.gameObject == currentFinishTrigger)
    {
        Debug.Log($"🎯 Checking flag center in OnTriggerStay...");
        if (HasReachedTriggerCenter(other))
        {
            Debug.Log($"🎯🎯🎯 CENTER REACHED in OnTriggerStay!");
            StartCoroutine(CompleteLevelWithDelay(other.gameObject));
        }
    }
}

    /// <summary>Останавливает корутины прыжка/поворота/таймеров и сбрасывает флаги.
    /// Иначе после стопа во время прыжка JumpRoutine мог завершиться позже и снова включить движение и velocity.</summary>
    private void CancelJumpAndMovementCoroutines()
    {
        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }
        StopAllCoroutines();
        CancelInvoke();
        isJumping = false;
        isRotating = false;
        isOnDirectionTile = false;
        isOnJumpTile = false;
        lastDirectionTile = null;
        lastJumpTile = null;
        isColliding = false;
        currentFinishTrigger = null;

        if (_boxCollider != null)
        {
            _boxCollider.size = originalColliderSize;
            _boxCollider.center = originalColliderCenter;
        }
    }

    /// <summary>Только прыжок — без StopAllCoroutines, чтобы не оборвать CompleteLevelWithDelay.</summary>
    private void ForceEndJumpOnly()
    {
        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }
        isJumping = false;
        if (_boxCollider != null)
        {
            _boxCollider.size = originalColliderSize;
            _boxCollider.center = originalColliderCenter;
        }
    }

public void ForceStopAllMovement()
{
    CancelJumpAndMovementCoroutines();
    movementEnabled = false;
    
    // Останавливаем физику
    if (RB != null)
    {
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        RB.isKinematic = true; // Временная блокировка
    }
    
    // Снэпаем позицию
    transform.position = GetSnappedPosition(transform.position);
    
    Debug.Log("All movement force-stopped");
}

IEnumerator CompleteLevelWithDelay(GameObject finishTrigger)
{
    Debug.Log("🎮 LEVEL COMPLETE STARTED");

    ForceEndJumpOnly();
    isRotating = false;
    
    // 1. Уничтожить флаг
    Destroy(finishTrigger);
    currentFinishTrigger = null;
    
    // 2. ВЫЗЫВАЕМ ВСЕ ГОТОВЫЕ МЕТОДЫ:
    
    // Останавливаем движение (существующий метод)
    DisableMovement();
    
    // Сбрасываем физику (существующий метод)
    ResetPhysics();
    
    // Сбрасываем speed boost (существующий метод)
    ResetSpeedBoost();
    
    // Сбрасываем цвета тайлов (если нужно)
    ResetAllTileColors();
    
    // Сбрасываем дополнительные флаги
    isJumping = false;
    isRotating = false;
    isColliding = false;
    
    // Делаем kinematic на всякий случай
    if (RB != null) RB.isKinematic = true;
    
    // Снэпаем позицию
    transform.position = GetSnappedPosition(transform.position);
    
    Debug.Log($"⏳ Waiting {levelCompleteDelay}s...");
    
    // 3. Ждем задержку
    yield return new WaitForSeconds(levelCompleteDelay);
    
    // 4. Показываем UI
    if (levelCompleteUI != null)
    {
        levelCompleteUI.SetActive(true);
        Debug.Log("✅ UI SHOWN");
    }
    
    Debug.Log("🎮 LEVEL COMPLETE FINISHED");
}
    void OnTriggerExit(Collider other)
{
    if (editModeChecker != null && editModeChecker.isInEditMode) return;
    
    if (other.CompareTag(jumpTileTag))
    {
        Debug.Log($"🚪 Выход из прыжкового тайла {other.name} | Позиция: {transform.position}");
        isOnJumpTile = false;
        lastJumpTile = null;
    }
    
    if (other.CompareTag(directionTileTag))
    {
        Debug.Log($"🚪 Выход из поворотного тайла {other.name} | Позиция: {transform.position}");
        isOnDirectionTile = false;
        lastDirectionTile = null;
    }
    
    if (other.gameObject == currentFinishTrigger)
    {
        currentFinishTrigger = null;
    }
}

    public bool HasReachedTriggerCenter(Collider trigger)
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
    isDead = false;
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
    Debug.Log("=== STOP GAME ===");

    // Сбрасываем в начальное состояние (позиция, вращение, физика, остановка, очистка фрагментов)
    ResetToInitialState();
    
    // Сбрасываем цвета тайлов
    ResetAllTileColors();
    ResetSpeedBoost();
    ResetAllFragileTiles();
    
    ResetAllResettableObjects();
    
    Debug.Log("Игра остановлена и сброшена к старту");
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
    // Используем FindAnyObjectByType - он быстрее
    DickControlledCube cube = FindAnyObjectByType<DickControlledCube>();
    if (cube != null)
    {
        cube.StopGame();
    }
    else
    {
        Debug.LogWarning("No DickControlledCube found in scene!");
    }
}

public void FullReset() {
    StopAllCoroutines();
    isDead = false;
    isRotating = false;
    isGrounded = true;
    movementEnabled = false;
    
    // ← ДОБАВИТЬ: Сброс флага
    currentFinishTrigger = null;
    
    // Сбрасываем ускорение
    ResetSpeedBoost();
    
    // Восстанавливаем хрупкие тайлы
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
        isDead = false;
        isExploded = false; // На всякий случай сбрасываем здесь тоже
        movementEnabled = false; // Всегда останавливаем при сбросе
        CancelJumpAndMovementCoroutines();
        
        // Удаляем фрагменты если они были
        ClearExistingFragments();
        
        // Показываем оригинальный куб
        SetCubeVisible(true);
        
        transform.position = InitialPosition;
        transform.rotation = initialRotation;
        RB.linearVelocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        RB.linearDamping = rigidbodyLinearDamping;
        RB.angularDamping = rigidbodyAngularDamping;
        currentDirection = mainPointer.forward;
        isRotating = false;
        isGrounded = true;
        RB.freezeRotation = true;
        RB.useGravity = true;
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
    if (isJumping) return;

    Vector3 moveDir = currentDirection.sqrMagnitude > 1e-6f ? currentDirection.normalized : Vector3.forward;

    bool hasObstacle = Physics.Raycast(
        transform.position, 
        moveDir, 
        checkDistance, 
        collisionLayers);

    if (!hasObstacle)
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, moveDir, out hit, checkDistance))
        {
            Bomb bomb = hit.collider.GetComponent<Bomb>();
            if (bomb != null)
            {
                hasObstacle = true;
                if (!bomb.isActivated)
                    StartCoroutine(bomb.QuickExplode());
            }
        }
    }

    if (hasObstacle)
    {
        if (!isColliding)
            StartCollision();
        RB.linearVelocity = Vector3.zero;
    }
    else
    {
        if (isColliding)
            EndCollision();
        RB.linearVelocity = moveDir * speed;
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
}

    void OnDisable()
{
    CancelInvoke();
    EndCollision();
    
    // ← ДОБАВИТЬ: Сброс ссылки на флаг
    currentFinishTrigger = null;
    
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
         Debug.Log($"🔄 RotateToDirection START | from={currentDirection} to={newDirection}");
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
        isGrounded = CheckGround();
        if (isJumping) return;
        if (!isGrounded) StartFalling();
    }

    bool CheckGround()
    {
        if (_boxCollider != null)
        {
            Vector3 bottom = transform.TransformPoint(_boxCollider.center + Vector3.down * _boxCollider.size.y);
            Vector3 origin = bottom + Vector3.up * 0.02f;
            float rayLen = groundCheckDistance + 0.08f;
            Debug.DrawRay(origin, Vector3.down * rayLen, Color.red, 0.5f);
            if (Physics.Raycast(origin, Vector3.down, rayLen, groundMask, QueryTriggerInteraction.Ignore))
                return true;
        }

        Vector3 centerOrigin = transform.position;
        Debug.DrawRay(centerOrigin, Vector3.down * groundCheckDistance, Color.yellow, 0.5f);
        return Physics.Raycast(centerOrigin, Vector3.down, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    void StartFalling()
    {
        RB.freezeRotation = false;
        RB.useGravity = true;
    }

    void CheckFallDeath()
    {
        if (!enableFallDeath || isDead) return;
        if (editModeChecker != null && editModeChecker.isInEditMode) return;

        float y = transform.position.y;
        if (y < fallDeathWorldY || y < InitialPosition.y - maxFallBelowSpawnY)
            GameOver();
    }

  void SnapToGrid()
    {
        Vector3 snappedPos = GetSnappedPosition(transform.position);
        snappedPos.y = transform.position.y;
        RB.MovePosition(snappedPos);
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
    RB.useGravity = true;
    RB.linearVelocity = Vector3.zero;
    RB.angularVelocity = Vector3.zero;
    
    SnapToGrid();
}
}