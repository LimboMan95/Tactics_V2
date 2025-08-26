using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[RequireComponent(typeof(DickControlledCube))]
public class GridObjectMover : MonoBehaviour
{
    [Header("Layer Settings")]
    public LayerMask movableLayer; // Слой для перемещаемых объектов
    public LayerMask rotatableLayer; // Слой для вращаемых объектов
    public LayerMask staticObstaclesLayer; // Слой статических препятствий

    [Header("Edit Mode Settings")]
    public KeyCode editModeKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;
    public bool startInEditMode = true;

    [Header("Movement Settings")]
    public float raycastDistance = 100f;
    public float tileSize = 1f;

    [Header("Rotation Settings")]
    public float rotationDuration = 0.3f;
    public Button rotateButton;
    public Image rotationIndicator;
    public Sprite[] directionSprites;

    [Header("Visual Feedback")]
    public Color highlightColor = Color.yellow;
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.7f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.7f);
    public Color rotationColor = new Color(1f, 0.8f, 0.2f, 0.7f);

    private Camera mainCamera;
    private DickControlledCube cubeController;
    private GameObject selectedObject;
    private Vector3 originalObjectPosition;
    private int currentRotationIndex;
    private bool isRotating;
    private bool isInEditMode;
    private Renderer[] objectRenderers;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private enum SelectionMode { None, Clicked, Dragging }
    private SelectionMode currentSelectionMode = SelectionMode.None;
    private bool isDragging = false;
    private bool isPermanentlySelected = false;


    private void Awake()
    {
        mainCamera = Camera.main;
        cubeController = GetComponent<DickControlledCube>();
        tileSize = cubeController.tileSize;
    }

    private IEnumerator Start()
    {
        yield return null;
        InitializeUI();
        if (startInEditMode && CanEnterEditMode()) StartEditMode();
    }
private void Update()
{
    HandleEditModeToggle();
    if (isInEditMode)
    {
        // Убираем дублирующий HandleSelection
        HandleObjectSelection(); // Теперь используем только этот метод
        HandleObjectMovement();
        HandleRotationInput();
    }
    
    if (Input.GetMouseButtonDown(0))
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 100))
        {
            Debug.Log($"Попал в: {hit.collider.name}", hit.collider.gameObject);
        }
        else
        {
            Debug.Log("Не попал ни во что");
        }

    }
     }

   #region Selection System

    public bool IsPointerOverUI()
{
    // Если нет EventSystem, считаем что UI не мешает
    if (EventSystem.current == null)
    {
        Debug.LogWarning("EventSystem not found!");
        return false;
    }

    // Для мобильных устройств
    if (Input.touchCount > 0)
    {
        return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
    }
    
    // Для редактора и мыши
    #if UNITY_EDITOR
    if (Input.GetMouseButtonDown(0)) // Только в момент клика
    #endif
    return EventSystem.current.IsPointerOverGameObject();
    
    return false;
}

    private void StartDragging()
    {
        if (!isInEditMode || selectedObject == null) return;
        
        isDragging = true;
        isPermanentlySelected = false;
        originalObjectPosition = selectedObject.transform.position;
    }

    private void HandleDragging()
    {
        if (!isDragging || selectedObject == null) return;

        if (Input.GetMouseButton(0))
        {
            // Процесс перетаскивания
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
            {
                Vector3 newPos = GetSnappedPosition(hit.point);
                newPos.y = selectedObject.transform.position.y;
                selectedObject.transform.position = newPos;
                UpdateObjectVisuals(IsPositionValid(newPos));
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // Завершение перетаскивания
            isDragging = false;
            
            if (!IsPositionValid(selectedObject.transform.position))
            {
                selectedObject.transform.position = originalObjectPosition;
            }
            
            // После перетаскивания объект остается выбранным
            isPermanentlySelected = true;
            UpdateObjectVisuals(true);
        }
    }
    #endregion

    #region UI Methods
    private void InitializeUI()
    {
        if (rotateButton) rotateButton.onClick.AddListener(RotateSelectedObject);
        UpdateUIState(false);
    }

    private void UpdateUIState(bool active)
    {
        if (rotateButton) rotateButton.interactable = active;
        if (rotationIndicator) rotationIndicator.gameObject.SetActive(active);
    }
    #endregion

    #region Edit Mode Control
    private void HandleEditModeToggle()
    {
        if (Input.GetKeyDown(editModeKey)) ToggleEditMode();
        if (isInEditMode && Input.GetKeyDown(cancelKey)) StopEditMode();
    }

    public void ToggleEditMode()
    {
        if (isInEditMode) StopEditMode();
        else if (CanEnterEditMode()) StartEditMode();
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
        Debug.Log("Edit mode activated");
    }

    private void StopEditMode()
    {
        ResetSelection();
        isInEditMode = false;
        Debug.Log("Edit mode deactivated");
    }
    #endregion

    #region Object Selection
    private void HandleObjectSelection()
{
    if (!isInEditMode) return; // Добавлено
    if (Input.GetMouseButtonDown(0))
    {
        var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, raycastDistance, movableLayer))
        {
            if (selectedObject != hit.collider.gameObject)
            {
                ResetSelection();
                SelectObject(hit.collider.gameObject);
                currentSelectionMode = SelectionMode.Clicked;
            }
            else
            {
                currentSelectionMode = SelectionMode.Dragging;
                originalObjectPosition = selectedObject.transform.position;
            }
        }
        else if (isPermanentlySelected)
        {
            ResetSelection();
        }
    }
}

   private void SelectObject(GameObject obj)
{
    if (obj == null)
    {
        Debug.LogError("SelectObject: obj is null!");
        return;
    }

    try
    {
        selectedObject = obj;
        originalObjectPosition = obj.transform.position;
        Debug.Log($"Выбран: {obj.name}");

        // Проверка рендереров
        objectRenderers = obj.GetComponentsInChildren<Renderer>();
        Debug.Log($"Найдено рендереров: {objectRenderers.Length}");
        
        originalMaterials.Clear();
        foreach (var renderer in objectRenderers)
        {
            if (renderer == null) continue;
            originalMaterials[renderer] = renderer.materials;
        }

        // Проверка вращения
        bool isRotatable = ((1 << obj.layer) & rotatableLayer) != 0;
        Debug.Log($"isRotatable: {isRotatable}");
        
        UpdateUIState(isRotatable);
        
        if (isRotatable)
        {
            CalculateCurrentRotationIndex();
            UpdateRotationVisual();
        }

        UpdateObjectVisuals(true);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Ошибка в SelectObject: {e}");
    }
}

    private void ResetSelection()
    {
        if (isDragging) return;

        RestoreOriginalMaterials();
        UpdateUIState(false);
        selectedObject = null;
        isPermanentlySelected = false;
        isDragging = false;
    }
    #endregion

    #region Object Manipulation
     private void HandleObjectMovement()
{
    if (!isInEditMode || selectedObject == null || currentSelectionMode != SelectionMode.Dragging) 
        return;

        if (Input.GetMouseButton(0))
        {
            // Процесс перетаскивания
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, raycastDistance))
            {
                Vector3 newPos = GetSnappedPosition(hit.point);
                newPos.y = selectedObject.transform.position.y;
                selectedObject.transform.position = newPos;
                UpdateObjectVisuals(IsPositionValid(newPos));
            }
        }
        else if (Input.GetMouseButtonUp(0) && currentSelectionMode == SelectionMode.Dragging)
        {
            // Завершение перетаскивания
            if (!IsPositionValid(selectedObject.transform.position))
            {
                selectedObject.transform.position = originalObjectPosition;
            }
            currentSelectionMode = SelectionMode.Clicked;
            UpdateObjectVisuals(true);
        }
    }


    private void HandleRotationInput()
{
    if (!isInEditMode || selectedObject == null || !Input.GetKeyDown(KeyCode.R)) 
        return;
    
    RotateSelectedObject();
}

    public void RotateSelectedObject()
{
    if (!isInEditMode || selectedObject == null || isRotating || ((1 << selectedObject.layer) & rotatableLayer) == 0) 
        return;
    
    StartCoroutine(RotateObjectCoroutine(90f));
}

    private IEnumerator RotateObjectCoroutine(float angle)
    {
        isRotating = true;
        UpdateObjectVisuals(false, true);

        Quaternion startRotation = selectedObject.transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, angle, 0);
        float elapsed = 0f;
        
        while (elapsed < rotationDuration)
        {
            selectedObject.transform.rotation = Quaternion.Slerp(
                startRotation, 
                endRotation, 
                elapsed / rotationDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        selectedObject.transform.rotation = endRotation;
        currentRotationIndex = (currentRotationIndex + 1) % 4;
        UpdateRotationVisual();
        
        UpdateObjectVisuals(IsPositionValid(selectedObject.transform.position), false);
        isRotating = false;
    }
    #endregion

    #region Visual Feedback
    private void UpdateObjectVisuals(bool isValid, bool isRotating = false)
    {
        if (objectRenderers == null) return;

        foreach (var renderer in objectRenderers)
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = new Material(renderer.materials[i]);
                newMaterials[i].color = isRotating ? rotationColor : (isValid ? validColor : invalidColor);
                SetMaterialTransparency(newMaterials[i]);
            }
            renderer.materials = newMaterials;
        }
    }

    private void SetMaterialTransparency(Material mat)
    {
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
        originalMaterials.Clear();
    }

    private void UpdateRotationVisual()
    {
        if (rotationIndicator != null && directionSprites != null && directionSprites.Length >= 4)
        {
            rotationIndicator.sprite = directionSprites[currentRotationIndex];
        }
    }

    private void CalculateCurrentRotationIndex()
    {
        if (selectedObject == null) return;
        float angle = selectedObject.transform.eulerAngles.y;
        currentRotationIndex = Mathf.RoundToInt(angle / 90f) % 4;
    }
    #endregion

    #region Utility Methods
    private Vector3 GetSnappedPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / tileSize) * tileSize,
            position.y,
            Mathf.Round(position.z / tileSize) * tileSize
        );
    }

    private bool IsPositionValid(Vector3 position)
    {
         if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, 1f, cubeController.groundMask))
    {
        return false; // Нет земли под объектом
    }
        
        Vector3 checkPos = position + Vector3.up * 0.1f;
        Collider[] colliders = Physics.OverlapBox(checkPos, Vector3.one * (tileSize * 0.45f));

        foreach (var col in colliders)
        {
            if (col.gameObject == selectedObject || 
                ((1 << col.gameObject.layer) & cubeController.groundMask) != 0)
                continue;

            if (((1 << col.gameObject.layer) & staticObstaclesLayer) != 0)
                return false;

            if (((1 << col.gameObject.layer) & movableLayer) != 0)
                return false;
        }
        return true;
    }
    #endregion

    public void ForceEnableEditMode()
{
    // Если уже в режиме редактирования - ничего не делаем
    if (isInEditMode) return;
    
    // Принудительно включаем режим редактирования
    isInEditMode = true;
    
    // Дополнительные действия при включении
    Debug.Log("Edit mode FORCED ON");
    
    // Сбрасываем выделение при включении
    ResetSelection();
}

// Этот метод будет привязан к кнопке "Выключить редактирование"
public void ForceDisableEditMode()
{
    // Если уже не в режиме редактирования - ничего не делаем
    if (!isInEditMode) return;
    
    // Принудительно выключаем режим редактирования
    isInEditMode = false;
    
    // Дополнительные действия при выключении
    Debug.Log("Edit mode FORCED OFF");
    
    // Сбрасываем выделение при выключении
    ResetSelection();
    
    // Если объект был в процессе перетаскивания - возвращаем на место
    if (selectedObject != null && isDragging)
    {
        selectedObject.transform.position = originalObjectPosition;
        isDragging = false;
    }
}
}