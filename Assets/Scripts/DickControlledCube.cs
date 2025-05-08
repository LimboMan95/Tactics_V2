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

    [Header("Visual Settings")]
    public float snapThreshold = 0.5f;
    public Color tileHighlightColor = Color.cyan;
    public float highlightDuration = 0.5f;

    [Header("References")]
    public Transform mainPointer;
    public Transform visualPointer;
    [Header("Movement Control")]
    public bool movementEnabled = true; // Новый флаг
     public Vector3 currentDirection = Vector3.forward; // Явная инициализация


    private Rigidbody rb;
    private bool isRotating = false;
    [SerializeField] private bool isGrounded = true;
    private Vector3 lastGridPosition;
    private GameObject lastHighlightedTile;
    private Color originalTileColor;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 visualPointerLocalPosition;
    private Quaternion visualPointerLocalRotation;

    public bool IsGrounded => isGrounded;
    public Vector3 CurrentDirection => currentDirection;
    public float CurrentSpeed => speed;

    void Awake()
{
    if(mainPointer != null) 
        currentDirection = mainPointer.forward;
}
    void Start()
    {
        
         // Гарантированная инициализация направления
        if(mainPointer != null && currentDirection == Vector3.zero)
        {
            currentDirection = mainPointer.forward;
        }

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false;

        // Сохраняем начальное состояние
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
    }
    void Update()
{
    if (Input.GetKeyDown(KeyCode.T))
    {
        StartCoroutine(RotateToDirection(Vector3.forward));
    }
}

    void FixedUpdate()
{
    // Первым делом проверяем grounded состояние (как в старой версии)
    isGrounded = CheckGround();
    
    if (!isGrounded)
    {
        StartFalling();
        return;
    }
    
    // Если не в процессе вращения - обрабатываем движение
    if (!isRotating)
    {
        CheckDirectionTileUnderneath();
        HandleMovement();
    }
    
    // Добавляем обновление указателей из старой версии
    if (visualPointer != null && mainPointer != null)
    {
        visualPointer.position = Vector3.Lerp(
            visualPointer.position,
            mainPointer.TransformPoint(visualPointerLocalPosition),
            Time.fixedDeltaTime * 20f
        );
        
        visualPointer.rotation = Quaternion.Slerp(
            visualPointer.rotation,
            mainPointer.rotation * visualPointerLocalRotation,
            Time.fixedDeltaTime * 20f
        );
    }
     if (!movementEnabled) 
    {
        rb.linearVelocity = Vector3.zero;
        return;
    }
}

    public void ToggleMovement()
    {
        movementEnabled = !movementEnabled;
        Debug.Log($"Movement {(movementEnabled ? "ENABLED" : "DISABLED")}");
    }
    
    public void DisableMovement()
    {
        movementEnabled = false;
        
        // Дополнительно останавливаем физику
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        Debug.Log("Movement FORCED to FALSE");
    }

    public void UpdateDirection(Vector3 newDirection)
{
    currentDirection = newDirection;
    if(mainPointer != null) mainPointer.forward = newDirection;
    if(visualPointer != null) visualPointer.forward = newDirection;
}

public void ForceUpdateDirection(Vector3 newDirection)
    {
        currentDirection = newDirection.normalized;
        if(mainPointer != null) mainPointer.forward = currentDirection;
        if(visualPointer != null) visualPointer.forward = currentDirection;
    }

    /// <summary> Сбрасывает куб в начальное состояние </summary>
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
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f))
        {
            if (hit.collider.CompareTag(directionTileTag))
            {
                Vector3 tileDirection = hit.transform.forward;
                
                if (Vector3.Angle(currentDirection, tileDirection) > 5f)
                {
                    if (HasPassedHalfCell())
                    {
                        StartCoroutine(RotateToDirection(tileDirection));
                        return;
                    }
                }
            }
        }

        if (!Physics.Raycast(transform.position, currentDirection, checkDistance, obstacleMask))
        {
            rb.linearVelocity = currentDirection * speed;
        }
        else
        {
            StartCoroutine(RotateOnCollision());
        }
    }

 IEnumerator RotateToDirection(Vector3 newDirection)
{
    isRotating = true;
    rb.linearVelocity = Vector3.zero;

    Quaternion startRotation = transform.rotation;
    Quaternion targetRotation = Quaternion.LookRotation(newDirection);
    float elapsed = 0f;

    while (elapsed < 1f)
    {
        // Возвращаем старый подход с transform.rotation
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
    mainPointer.rotation = transform.rotation;
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

    bool CheckGround()
    {
        float halfSize = cubeSize * 0.5f * transform.localScale.x;
        Vector3[] checkPoints = new Vector3[]
        {
            transform.position,
            transform.position + transform.TransformDirection(new Vector3(-halfSize, 0, -halfSize)),
            transform.position + transform.TransformDirection(new Vector3(-halfSize, 0, halfSize)),
            transform.position + transform.TransformDirection(new Vector3(halfSize, 0, -halfSize)),
            transform.position + transform.TransformDirection(new Vector3(halfSize, 0, halfSize))
        };

        int hits = 0;
        foreach (Vector3 point in checkPoints)
        {
            if (Physics.Raycast(point, Vector3.down, groundCheckDistance, groundMask))
            {
                hits++;
                Debug.DrawRay(point, Vector3.down * groundCheckDistance, Color.green, 1f);
            }
            else
            {
                Debug.DrawRay(point, Vector3.down * groundCheckDistance, Color.red, 1f);
            }
        }
        return hits >= 3;
    }

    void StartFalling()
    {
        rb.freezeRotation = false;
        rb.useGravity = true;
    }

    void SnapToGrid()
    {
        Vector3 snappedPos = GetSnappedPosition(transform.position);
        snappedPos.y = transform.position.y;
        transform.position = snappedPos;
        lastGridPosition = snappedPos;
    }

    Vector3 GetSnappedPosition(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x),
            Mathf.Round(pos.y),
            Mathf.Round(pos.z)
        );
    }

    bool HasPassedHalfCell()
    {
        Vector3 delta = transform.position - lastGridPosition;
        return Mathf.Abs(delta.x) >= snapThreshold || Mathf.Abs(delta.z) >= snapThreshold;
    }
}