using UnityEngine;

[RequireComponent(typeof(DickControlledCube))]
public class GridObjectMover : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask movableObjectsLayer;
    public LayerMask levelDesignLayer;
    public KeyCode editModeKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;
    public float raycastDistance = 100f;
    
    [Header("References")]
    [SerializeField] private DickControlledCube cubeController;
    [SerializeField] private Camera mainCamera;
    
    private GameObject selectedObject;
    private Vector3 lastObjectPosition;
    private bool isInEditMode;
    private float tileSize;

    private void Awake()
    {
        if (!cubeController) cubeController = GetComponent<DickControlledCube>();
        if (!mainCamera) mainCamera = Camera.main;
        tileSize = cubeController.tileSize;
    }

    private void Update()
    {
        HandleEditModeToggle();
        
        if (isInEditMode)
        {
            HandleObjectSelection();
            HandleObjectMovement();
        }
    }

    private void HandleEditModeToggle()
    {
        if (Input.GetKeyDown(editModeKey))
        {
            if (!isInEditMode && CanEnterEditMode())
            {
                StartEditMode();
            }
            else if (isInEditMode)
            {
                StopEditMode();
            }
        }

        if (isInEditMode && Input.GetKeyDown(cancelKey))
        {
            StopEditMode();
        }
    }

    private bool CanEnterEditMode()
    {
        return cubeController.transform.position == cubeController.InitialPosition && 
               cubeController.RB.linearVelocity.magnitude < 0.1f && 
               !cubeController.IsMovementEnabled;
    }

    private void StartEditMode()
    {
        isInEditMode = true;
        Debug.Log("Режим редактирования активирован");
        // Можно добавить визуальную индикацию
    }

    private void StopEditMode()
    {
        isInEditMode = false;
        selectedObject = null;
        Debug.Log("Режим редактирования отключен");
    }

    private void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0)) // Изменили на GetMouseButtonDown
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, movableObjectsLayer))
            {
                selectedObject = hit.collider.gameObject;
                lastObjectPosition = selectedObject.transform.position;
                Debug.Log($"Выбран объект: {selectedObject.name}", selectedObject);
            }
        }
    }

    private void HandleObjectMovement()
    {
        if (!selectedObject) return;

        if (Input.GetMouseButton(0)) // Удерживаем ЛКМ для перемещения
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, cubeController.groundMask))
            {
                Vector3 newPos = GetSnappedPosition(hit.point);
                newPos.y = selectedObject.transform.position.y; // Сохраняем высоту
                
                if (IsTileFree(newPos))
                {
                    selectedObject.transform.position = newPos;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0)) // Отпустили ЛКМ
        {
            if (!IsTileFree(selectedObject.transform.position))
            {
                selectedObject.transform.position = lastObjectPosition;
                Debug.Log("Нельзя разместить здесь - тайл занят!");
            }
            selectedObject = null;
        }
    }

    private Vector3 GetSnappedPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / tileSize) * tileSize,
            position.y,
            Mathf.Round(position.z / tileSize) * tileSize
        );
    }

    private bool IsTileFree(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(
            position, 
            Vector3.one * (tileSize * 0.45f), 
            Quaternion.identity, 
            levelDesignLayer);

        return colliders.Length == 0;
    }
    private void OnDrawGizmos()
{
    if (!isInEditMode) return;
    
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireCube(transform.position, Vector3.one * tileSize);
    
    if (selectedObject)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(mainCamera.transform.position, selectedObject.transform.position);
    }
}
}