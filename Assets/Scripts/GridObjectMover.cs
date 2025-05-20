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
    public bool startInEditMode = true;
    
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
    private Renderer[] highlightedRenderers;
    private Material[][] originalMaterials;

    [Header("Direction Tile Settings")]
public bool preventDirectionTileOverlap = true;
public LayerMask directionTileLayer;

    private void Awake()
    {
        if (!cubeController) cubeController = GetComponent<DickControlledCube>();
        if (!mainCamera) mainCamera = Camera.main;
        tileSize = cubeController.tileSize;
        directionTileLayer = LayerMask.GetMask("Tools");
    }

    private IEnumerator Start()
    {
        // Ждем один кадр, чтобы все компоненты инициализировались
        yield return null;
        
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

     public void ToggleEditMode()
    {
        if (isInEditMode)
        {
            StopEditMode();
        }
        else if (CanEnterEditMode())
        {
            StartEditMode();
        }
        else
        {
            Debug.Log("Не могу войти в режим редактирования: не выполнены условия");
        }
    }
    
     public void TurnOffWithButtonEditMode()
    {
            if (isInEditMode)
        {
            StopEditMode();
        }
    }

 public void ForceEnableEditMode()
    {
        if (!isInEditMode && CanEnterEditMode())
        {
            StartEditMode();
        }
        else if (isInEditMode)
        {
            Debug.Log("Режим редактирования уже активен");
        }
        else
        {
            Debug.LogWarning("Не могу принудительно включить режим: не выполнены условия");
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
                ResetObjectHighlight();
                
                selectedObject = hit.collider.gameObject;
                lastObjectPosition = selectedObject.transform.position;
                
                HighlightObject(selectedObject);
            }
        }
    }

     private void HighlightObject(GameObject obj)
    {
        // Получаем все рендереры объекта и его детей
        highlightedRenderers = obj.GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[highlightedRenderers.Length][];
        
        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            // Сохраняем оригинальные материалы
            originalMaterials[i] = highlightedRenderers[i].materials;
            
            // Создаем новые материалы для подсветки
            Material[] highlightMats = new Material[highlightedRenderers[i].materials.Length];
            for (int j = 0; j < highlightMats.Length; j++)
            {
                highlightMats[j] = new Material(originalMaterials[i][j]);
                highlightMats[j].color = highlightColor;
            }
            
            highlightedRenderers[i].materials = highlightMats;
        }
        
        // Запускаем таймер сброса подсветки
        StartCoroutine(ResetHighlightAfterDelay(highlightDuration));
    }

     private IEnumerator ResetHighlightAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetObjectHighlight();
    }

    private void ResetObjectHighlight()
    {
        if (highlightedRenderers != null)
        {
            for (int i = 0; i < highlightedRenderers.Length; i++)
            {
                if (highlightedRenderers[i] != null && originalMaterials != null && originalMaterials[i] != null)
                {
                    highlightedRenderers[i].materials = originalMaterials[i];
                }
            }
        }
        
        highlightedRenderers = null;
        originalMaterials = null;
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

                 // Дополнительная проверка для поворотных тайлов
            if (
                selectedObject.layer == LayerMask.NameToLayer("Tools"))
            {
                if (IsDirectionTileOverlap(newPos))
                {
                    Debug.Log("Нельзя размещать поворотные тайлы друг на друга!");
                    return;
                }
            }
                
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

    private bool IsDirectionTileOverlap(Vector3 position)
{
    Collider[] directionTiles = Physics.OverlapBox(
        position,
        Vector3.one * (tileSize * 0.45f),
        Quaternion.identity,
        LayerMask.GetMask("Tools"));

    return directionTiles.Length > 0;
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
    Collider[] allColliders = Physics.OverlapBox(
        position, 
        Vector3.one * (tileSize * 0.45f));

    foreach (var collider in allColliders)
    {
        // Пропускаем триггеры и сам объект
        if (collider.isTrigger || 
            (selectedObject != null && collider.gameObject == selectedObject))
            continue;

        // Проверка статических препятствий
        if (((1 << collider.gameObject.layer) & levelDesignLayer) != 0)
            return false;

        // Проверка других перемещаемых объектов
        if (((1 << collider.gameObject.layer) & movableObjectsLayer) != 0)
            return false;

        // Специальная проверка для поворотных тайлов (по тегу и слою)
        if (collider.gameObject.layer == LayerMask.NameToLayer("Tools"))
            return false;
    }

    return true;
}

    private void OnDestroy()
    {
        // При уничтожении скрипта сбрасываем подсветку
        ResetObjectHighlight();
    }
}