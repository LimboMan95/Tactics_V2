using UnityEngine;
using System.Collections;

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

    [Header("References")]
    public Transform mainPointer;
    public Transform visualPointer;

    [Header("Movement Control")]
    public bool movementEnabled = true;
    public Vector3 currentDirection = Vector3.forward;

    private Rigidbody rb;
    [SerializeField] private bool isRotating = false;
    [SerializeField] private bool isGrounded = true;
    private Vector3 lastGridPosition;
    private GameObject lastHighlightedTile;
    private Color originalTileColor;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 visualPointerLocalPosition;
    private Quaternion visualPointerLocalRotation;
    private GameObject lastDirectionTile;
    private Vector3 entryPoint;
    private float halfTileSize;
    private Vector3 tileEntryPoint;
    private bool isOnDirectionTile;
    public bool IsGrounded => isGrounded;

    void Awake()
    {
        if(mainPointer != null) 
            currentDirection = mainPointer.forward;
    }

    void Start()
    {
        if(mainPointer != null && currentDirection == Vector3.zero)
        {
            currentDirection = mainPointer.forward;
        }

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false;

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (visualPointer != null && mainPointer != null)
        {
            visualPointerLocalPosition = mainPointer.InverseTransformPoint(visualPointer.position);
            visualPointerLocalRotation = Quaternion.Inverse(mainPointer.rotation) * visualPointer.rotation;
        }

        currentDirection = mainPointer.forward;
        lastGridPosition = GetSnappedPosition(transform.position);
        isGrounded = CheckGround();
        halfTileSize = tileSize / 2f;
        Debug.Log($"Rigidbody: isKinematic={rb.isKinematic}, UseGravity={rb.useGravity}, Drag={rb.linearDamping}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(RotateToDirection(Vector3.forward));
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

        // 1. Обработка тайлов направления
        CheckDirectionTileUnderneath();
        
        // 2. Поворот при прохождении половины тайла
        if (isOnDirectionTile && !isRotating && lastDirectionTile != null) {
        // Вычисляем пройденное расстояние по направлению движения
        float distance = Vector3.Dot(transform.position - tileEntryPoint, currentDirection);
        
        // Поворачиваем только после прохождения половины тайла
        if (distance >= tileSize * 0.5f) { // tileSize - размер тайла
            Vector3 tileDirection = lastDirectionTile.transform.forward;
            if (Vector3.Angle(currentDirection, tileDirection) > 5f) {
                StartCoroutine(RotateToDirection(tileDirection));
                isOnDirectionTile = false; // Предотвращаем повторный поворот
            }
        }
    }

        // 3. Основное движение
        if (movementEnabled && !isRotating) {
        if (isGrounded) {
            HandleMovement();
        }
        else if (!movementEnabled)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        } 
        }
         Debug.Log($"Movement: enabled={movementEnabled}, rotating={isRotating}, grounded={isGrounded}");
    }


//void HandleMovementState()
//{
    //if (!isGrounded || rb.isKinematic) return;

    //if (movementEnabled && !isRotating)
    //{
        // Убрать снэппинг на время движения (или использовать rb.MovePosition)
    // if (ShouldSnapToGrid()) SnapToGrid(); 

        // Только если нет препятствий впереди
        // if (!Physics.Raycast(transform.position, currentDirection, checkDistance, obstacleMask)) {
        //rb.linearVelocity = currentDirection * speed;
   // }
    //else {
       // rb.linearVelocity = Vector3.zero;
       // StartCoroutine(RotateOnCollision());
   // }
   // }
   // else // Если движение выключено или вращаемся
   // {
    //    rb.linearVelocity = Vector3.zero;
    //    rb.angularVelocity = Vector3.zero;
    //}
//}

 void OnTriggerEnter(Collider other) {
    if (!other.CompareTag(directionTileTag)) return;
    
    lastDirectionTile = other.gameObject;
    tileEntryPoint = transform.position; // Фиксируем точку входа
    isOnDirectionTile = true;
    // Убрали немедленный поворот!
}

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(directionTileTag))
        {
            isOnDirectionTile = false;
            lastDirectionTile = null;
        }
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
    
   public void DisableMovement()
{
    movementEnabled = false;
    
    if (TryGetComponent<Rigidbody>(out var rb))
    {
        // Для kinematic bodies только останавливаем вращение
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
        }
        rb.angularVelocity = Vector3.zero;
    }
}
public void ResetPhysics()
{
    if (TryGetComponent<Rigidbody>(out var rb))
    {
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
        }
        rb.angularVelocity = Vector3.zero;
        rb.freezeRotation = true;
    }
    isGrounded = CheckGround();
}

    public void UpdateDirection(Vector3 newDirection)
{
    currentDirection = newDirection;
    if(mainPointer != null) mainPointer.forward = newDirection;
    if(visualPointer != null) visualPointer.forward = newDirection;
}
public void Revive()
{
    if (TryGetComponent<Rigidbody>(out var rb))
    {
        rb.WakeUp();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    
    // Сброс цветового эффекта
    if (TryGetComponent<CollisionColorChanger>(out var colorChanger))
    {
        colorChanger.ResetCollisionEffect();
    }
    
    isGrounded = CheckGround();
}
public void FullReset() {
    StopAllCoroutines();
    isRotating = false;
    isGrounded = true;
    movementEnabled = false;
    
    if (TryGetComponent<Rigidbody>(out var rb)) {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;
        rb.freezeRotation = true;
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
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentDirection = mainPointer.forward;
        isRotating = false;
        isGrounded = true;
        rb.freezeRotation = true;
        rb.useGravity = false;
        
        // Сброс визуального указателя
        if (visualPointer != null && mainPointer != null)
        {
            visualPointer.position = mainPointer.TransformPoint(visualPointerLocalPosition);
            visualPointer.rotation = mainPointer.rotation * visualPointerLocalRotation;
        }
    }

    void HandleMovement()
    {
        if (ShouldSnapToGrid())
        {
            SnapToGrid();
        }

        if (!Physics.Raycast(transform.position, currentDirection, checkDistance, obstacleMask))
        {
            rb.linearVelocity = currentDirection * speed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            StartCoroutine(RotateOnCollision());
        }
    }

// Новый вспомогательный метод для проверки тайлов
private bool CheckDirectionTileUnderneath(out Vector3 tileDirection)
{
    tileDirection = Vector3.zero;
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
        rb.linearVelocity = Vector3.zero;

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
        rb.linearVelocity = Vector3.zero;

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
                HighlightTile(hit.collider.gameObject);
            }
        }
    }

    void HighlightTile(GameObject tile)
    {
        if (lastHighlightedTile != null)
        {
            ResetTileColor(lastHighlightedTile);
        }

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            originalTileColor = tileRenderer.material.color;
            tileRenderer.material.color = tileHighlightColor;
            lastHighlightedTile = tile;
            Invoke(nameof(ResetLastTileColor), highlightDuration);
        }
    }

    void ResetTileColor(GameObject tile)
    {
        if (tile != null)
        {
            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = originalTileColor;
        }
    }

    void ResetLastTileColor()
    {
        ResetTileColor(lastHighlightedTile);
    }

    void PeriodicGroundCheck()
    {
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
        rb.freezeRotation = false;
        rb.useGravity = true;
    }

  void SnapToGrid() {
    // Снэп только при почти нулевой скорости
    if (rb.linearVelocity.magnitude < 0.1f) {
        Vector3 snappedPos = GetSnappedPosition(transform.position);
        snappedPos.y = transform.position.y;
        rb.MovePosition(snappedPos); // Плавное перемещение
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
}