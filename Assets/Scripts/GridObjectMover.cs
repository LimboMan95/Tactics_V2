using UnityEngine;
using System.Collections;

[RequireComponent(typeof(DickControlledCube))]
public class GridObjectMover : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask movableObjectsLayer;
    public LayerMask levelDesignLayer;
    public KeyCode editModeKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;
    public float raycastDistance = 100f;
    public bool startInEditMode = true; // Новая настройка
    
    [Header("Visuals")]
    public Color highlightColor = Color.yellow;
    public float highlightDuration = 2f;
    
    [Header("References")]
    [SerializeField] private DickControlledCube cubeController;
    [SerializeField] private Camera mainCamera;
    
    private GameObject selectedObject;
    private Vector3 lastObjectPosition;
    private bool isInEditMode;
    private float tileSize;
    private Material originalMaterial;
    private Coroutine highlightCoroutine;

    private void Awake()
    {
        if (!cubeController) cubeController = GetComponent<DickControlledCube>();
        if (!mainCamera) mainCamera = Camera.main;
        tileSize = cubeController.tileSize;
    }

    private void Start()
    {
        if (startInEditMode && CanEnterEditMode())
        {
            StartEditMode();
        }
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
        // Можно добавить дополнительную визуальную индикацию
    }

    private void StopEditMode()
    {
        isInEditMode = false;
        if (selectedObject != null)
        {
            ResetObjectHighlight();
        }
        selectedObject = null;
        Debug.Log("Режим редактирования отключен");
    }

    private void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, movableObjectsLayer))
            {
                // Сбрасываем предыдущее выделение
                if (selectedObject != null)
                {
                    ResetObjectHighlight();
                }
                
                selectedObject = hit.collider.gameObject;
                lastObjectPosition = selectedObject.transform.position;
                Debug.Log($"Выбран объект: {selectedObject.name}", selectedObject);
                
                // Подсвечиваем объект
                HighlightObject(selectedObject);
            }
        }
    }

    private void HighlightObject(GameObject obj)
    {
        // Отменяем предыдущую подсветку если была
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
        }
        
        // Запоминаем оригинальный материал
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            originalMaterial = renderer.material;
            
            // Создаем копию материала для подсветки
            Material highlightMat = new Material(originalMaterial);
            highlightMat.color = highlightColor;
            renderer.material = highlightMat;
            
            // Запускаем корутину для сброса подсветки
            highlightCoroutine = StartCoroutine(ResetHighlightAfterDelay(renderer, highlightDuration));
        }
    }

    private IEnumerator ResetHighlightAfterDelay(Renderer renderer, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (renderer != null && originalMaterial != null)
        {
            renderer.material = originalMaterial;
        }
        highlightCoroutine = null;
    }

    private void ResetObjectHighlight()
    {
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }
        
        if (selectedObject != null)
        {
            var renderer = selectedObject.GetComponent<Renderer>();
            if (renderer != null && originalMaterial != null)
            {
                renderer.material = originalMaterial;
            }
        }
    }

    private void HandleObjectMovement()
    {
        if (!selectedObject) return;

        if (Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, raycastDistance, cubeController.groundMask))
            {
                Vector3 newPos = GetSnappedPosition(hit.point);
                newPos.y = selectedObject.transform.position.y;
                
                if (IsTileFree(newPos))
                {
                    selectedObject.transform.position = newPos;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
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

    private void OnDestroy()
    {
        // При уничтожении скрипта сбрасываем подсветку
        ResetObjectHighlight();
    }
}