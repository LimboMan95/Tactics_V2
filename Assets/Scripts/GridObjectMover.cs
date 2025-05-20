using UnityEngine;
using System.Collections;
using System;

[RequireComponent(typeof(DickControlledCube))]
public class GridObjectMover : MonoBehaviour
{
    [Header("Settings")]
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

[Header("Visual Feedback")]
public Color validPlacementColor = Color.green;
public Color invalidPlacementColor = Color.red;
public float placementCheckInterval = 0.1f;
private Vector3 originalObjectPosition;
private bool isPositionValid;
private Renderer[] objectRenderers;
public Color validColor = new Color(0.2f, 1f, 0.2f, 0.7f); // Зеленый
public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.7f); // Красный

[Header("Collision Settings")]
public LayerMask movableObjectsLayer; // Общий слой для всех перемещаемых объектов (включая поворотные тайлы)
public LayerMask staticObstaclesLayer; // Слой статических препятствий


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
    if (selectedObject != null && !isPositionValid)
    {
        selectedObject.transform.position = originalObjectPosition;
    }
    
    ResetObjectColor();
    isInEditMode = false;
    selectedObject = null;
    StopAllCoroutines();
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
            if (selectedObject != null)
                ResetObjectAppearance();
            
            selectedObject = hit.collider.gameObject;
            originalObjectPosition = selectedObject.transform.position;
            
            objectRenderers = selectedObject.GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[objectRenderers.Length][];
            
            for (int i = 0; i < objectRenderers.Length; i++)
            {
                originalMaterials[i] = new Material[objectRenderers[i].materials.Length];
                System.Array.Copy(objectRenderers[i].materials, originalMaterials[i], objectRenderers[i].materials.Length);
            }
            
            var checkResult = CheckPositionValidity(selectedObject.transform.position);
            UpdateObjectVisuals(checkResult.isValid, checkResult.errorType);
        }
    }
}

private IEnumerator CheckPlacementValidity()
{
    while (selectedObject != null)
    {
        // Теперь проверка происходит в HandleObjectMovement
        yield return new WaitForSeconds(placementCheckInterval);
    }
}

private void UpdateObjectVisuals(bool isValid, string errorType)
{
    if (objectRenderers == null) return;

    Color targetColor = validColor;
    
    if (!isValid)
    {
        targetColor = errorType == "directionTile" ? 
            new Color(1f, 0.4f, 0f, 0.7f) : // Оранжевый для тайлов
            invalidColor; // Красный для остального
    }

    foreach (var renderer in objectRenderers)
    {
        Material[] tempMaterials = new Material[renderer.materials.Length];
        
        for (int i = 0; i < tempMaterials.Length; i++)
        {
            tempMaterials[i] = new Material(renderer.materials[i]);
            tempMaterials[i].color = targetColor;
            tempMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            tempMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            tempMaterials[i].EnableKeyword("_ALPHABLEND_ON");
            tempMaterials[i].renderQueue = 3000;
        }
        
        renderer.materials = tempMaterials;
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
            selectedObject.transform.position = newPos;
            
            var checkResult = CheckPositionValidity(newPos);
            UpdateObjectVisuals(checkResult.isValid, checkResult.errorType);
        }
    }
    else if (Input.GetMouseButtonUp(0))
    {
        var checkResult = CheckPositionValidity(selectedObject.transform.position);
        if (!checkResult.isValid)
        {
            selectedObject.transform.position = originalObjectPosition;
        }
        ResetObjectAppearance();
        selectedObject = null;
    }
}

private void ResetObjectAppearance()
{
    if (objectRenderers == null || originalMaterials == null) return;

    for (int i = 0; i < objectRenderers.Length; i++)
    {
        // Восстанавливаем оригинальные материалы
        objectRenderers[i].materials = originalMaterials[i];
        
        // Убедимся, что все оригинальные параметры шейдера восстановлены
        foreach (var mat in objectRenderers[i].materials)
        {
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = -1;
        }
    }
    
    // Очищаем ссылки
    objectRenderers = null;
    originalMaterials = null;
}

private void ResetObjectColor()
{
    if (objectRenderers != null && originalMaterials != null)
    {
        for (int i = 0; i < objectRenderers.Length; i++)
        {
            if (objectRenderers[i] != null)
            {
                objectRenderers[i].materials = originalMaterials[i];
            }
        }
    }
    
    objectRenderers = null;
    originalMaterials = null;
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

    private (bool isValid, string errorType) CheckPositionValidity(Vector3 position)
{
    Vector3 checkPos = position + Vector3.up * 0.1f;
    float checkSize = tileSize * 0.45f;
    
    Collider[] colliders = Physics.OverlapBox(checkPos, new Vector3(checkSize, 0.1f, checkSize));

    foreach (var col in colliders)
    {
        if (col.gameObject == selectedObject || 
            ((1 << col.gameObject.layer) & cubeController.groundMask) != 0)
            continue;

        // Статические препятствия
        if (((1 << col.gameObject.layer) & staticObstaclesLayer) != 0)
            return (false, "obstacle");

        // Другие перемещаемые объекты
        if (((1 << col.gameObject.layer) & movableObjectsLayer) != 0)
        return (false, "tool");
    }

    return (true, "valid");
}

    private void OnDestroy()
    {
        // При уничтожении скрипта сбрасываем подсветку
        ResetObjectHighlight();
    }
}