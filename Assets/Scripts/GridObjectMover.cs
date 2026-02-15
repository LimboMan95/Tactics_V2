using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[RequireComponent(typeof(DickControlledCube))]
public class GridObjectMover : MonoBehaviour
{
    [Header("Layer Settings")]
    public LayerMask movableLayer;
    public LayerMask rotatableLayer;
    public LayerMask staticObstaclesLayer;

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
    
    [Header("Debug")]
    public bool disableUIBlocking = true;
    [Header("Selection Settings")]
    public float selectionRadius = 0.5f;

    private Camera mainCamera;
    private DickControlledCube cubeController;
    private IsometricCameraRotator cameraRotator;
    private GameObject selectedObject;
    private Vector3 originalObjectPosition;
    private int currentRotationIndex;
    private bool isRotating;
    public bool isInEditMode;
    private Renderer[] objectRenderers;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private bool isObjectSelected = false;
    private bool isDragging = false;
    private bool isPermanentlySelected = false;

    private void Awake()
    {
        mainCamera = Camera.main;
        cubeController = GetComponent<DickControlledCube>();
        cameraRotator = mainCamera.GetComponent<IsometricCameraRotator>();
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
            HandleObjectSelection();
            HandleObjectMovement();
            HandleRotationInput();
            
            if (selectedObject != null && !isDragging && !isRotating)
            {
                bool isValid = IsPositionValid(selectedObject.transform.position);
                UpdateObjectVisuals(isValid);
            }
        }
    }

    #region Selection System

    public bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleObjectSelection()
    {
        if (!isInEditMode) return;
        
        if (Input.GetMouseButtonDown(0))
        {
        if (!disableUIBlocking && IsPointerOverUI()) return;
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, movableLayer))
            {
                ProcessSelection(hit.collider.gameObject);
                return;
            }
            
            Vector3 rayEndPoint = ray.origin + ray.direction * raycastDistance;
            Collider[] colliders = Physics.OverlapSphere(rayEndPoint, selectionRadius, movableLayer);
            
            if (colliders.Length > 0)
            {
                GameObject closest = null;
                float closestDist = float.MaxValue;
                
                foreach (var col in colliders)
                {
                    float dist = Vector3.Distance(rayEndPoint, col.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = col.gameObject;
                    }
                }
                
                ProcessSelection(closest);
            }
            else
            {
                Debug.Log("No objects found");
            }
        }
    }

    private void ProcessSelection(GameObject obj)
{
    if (obj == null) return;
    
    Debug.Log($"Selected: {obj.name}");
    
    if (selectedObject != obj)
    {
        ResetSelection();        // ← здесь selectedObject становится null
        SelectObject(obj);       // ← здесь selectedObject становится obj
    }
    
    // ← ПЕРЕНЕСИТЕ СЮДА!
    originalObjectPosition = selectedObject.transform.position;
    isDragging = true;
    isPermanentlySelected = false;
}

    private Ray GetCameraRay()
    {
        return mainCamera.ScreenPointToRay(Input.mousePosition);
    }

    private string GetCameraAngle()
    {
        if (cameraRotator != null)
        {
            try
            {
                System.Reflection.FieldInfo horizField = cameraRotator.GetType().GetField("currentHorizontalAngle", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.FieldInfo vertField = cameraRotator.GetType().GetField("currentVerticalAngle", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (horizField != null && vertField != null)
                {
                    float horiz = (float)horizField.GetValue(cameraRotator);
                    float vert = (float)vertField.GetValue(cameraRotator);
                    return $"H:{horiz:F1}°, V:{vert:F1}°";
                }
            }
            catch { }
        }
        
        Vector3 euler = mainCamera.transform.eulerAngles;
        return $"Transform angles: X:{euler.x:F1}°, Y:{euler.y:F1}°, Z:{euler.z:F1}°";
    }

    private void StartDragging()
    {
        if (!isInEditMode || selectedObject == null) return;
        
        isDragging = true;
        isPermanentlySelected = false;
        originalObjectPosition = selectedObject.transform.position;
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
        if (isInEditMode) 
        {
            StopEditMode();
        }
        else if (CanEnterEditMode()) 
        {
            StartEditMode();
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
        Debug.Log("Edit mode activated");
    }

    private void StopEditMode()
    {
        if (isDragging && selectedObject != null)
        {
            selectedObject.transform.position = originalObjectPosition;
        }
        
        ResetSelection();
        
        isDragging = false;
        isRotating = false;
        isPermanentlySelected = false;
        
        DickControlledCube cube = FindAnyObjectByType<DickControlledCube>();
        if (cube != null)
        {
            cube.ResetAllFragileTiles();
        }
        
        isInEditMode = false;
        
        Debug.Log("Edit mode deactivated - full reset");
    }
    #endregion

    #region Object Manipulation
    private void HandleObjectMovement()
    {
        if (!isInEditMode || selectedObject == null || !isDragging) 
            return;

        if (Input.GetMouseButton(0))
        {
            Ray ray = GetCameraRay();
            
            if (Physics.Raycast(ray, out var hit, raycastDistance))
            {
                Vector3 newPos = GetSnappedPosition(hit.point);
                newPos.y = selectedObject.transform.position.y;
                selectedObject.transform.position = newPos;
                UpdateObjectVisuals(IsPositionValid(newPos));
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            
            if (!IsPositionValid(selectedObject.transform.position))
            {
                selectedObject.transform.position = originalObjectPosition;
            }
            
            isPermanentlySelected = true;
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

// ← ДОБАВЬ ЭТО
public void RotateSelectedObject(float angle)
{
    if (!isInEditMode || selectedObject == null || isRotating || ((1 << selectedObject.layer) & rotatableLayer) == 0) 
        return;
    
    StartCoroutine(RotateObjectCoroutine(angle));
}

public void RotateSelectedObjectLeft()
{
    RotateSelectedObject(-90f);
}

public void RotateSelectedObjectRight()
{
    RotateSelectedObject(90f);
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
    if (selectedObject == null) 
    {
        Debug.LogWarning("UpdateObjectVisuals: selectedObject is null!");
        return;
    }
    
    if (objectRenderers == null)
    {
        Debug.LogWarning("objectRenderers is null!");
        return;
    }

    Debug.Log($"Updating visuals: isValid={isValid}, isRotating={isRotating}");

    foreach (var renderer in objectRenderers)
    {
        if (renderer == null) continue;
        
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

    public void ResetAllMaterialsInScene()
    {
        foreach (var renderer in FindObjectsOfType<Renderer>())
        {
            renderer.enabled = false;
            renderer.enabled = true;
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                foreach (var currentMat in kvp.Key.materials)
                {
                    if (currentMat != null && !System.Array.Exists(kvp.Value, m => m == currentMat))
                    {
                        Destroy(currentMat);
                    }
                }
                
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
    public Vector3 GetSnappedPosition(Vector3 position)
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
            return false;
        }
        
        Vector3 checkPos = position + Vector3.up * 0.1f;
        Collider[] colliders = Physics.OverlapBox(checkPos, Vector3.one * (tileSize * 0.45f));

        foreach (var col in colliders)
        {
            if (col.GetComponent<FragileTile>() != null) return false;
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
        if (isInEditMode) return;
        
        isInEditMode = true;
        
        Debug.Log("Edit mode FORCED ON");
        
        ResetSelection();
    }

    public void ForceDisableEditMode()
    {
        if (!isInEditMode) return;
        
        StopEditMode();
        
        Debug.Log("Edit mode FORCED OFF with full cleanup");
    }

    #region Object Selection Methods
    private void SelectObject(GameObject obj)
    {
        if (obj == null) return;
         // Сначала сбрасываем предыдущий выбор
        if (selectedObject != null && selectedObject != obj)
        {
            ResetSelection();
        }

        selectedObject = obj;
        originalObjectPosition = obj.transform.position;

        objectRenderers = obj.GetComponentsInChildren<Renderer>();
        if (objectRenderers == null || objectRenderers.Length == 0)
        {
            Debug.LogWarning($"No renderers found on {obj.name}");
            return;
        }

        originalMaterials.Clear();
        foreach (var renderer in objectRenderers)
        {
            if (renderer == null) continue;
            
            Material[] materialsCopy = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                materialsCopy[i] = new Material(renderer.materials[i]);
            }
            originalMaterials[renderer] = materialsCopy;
        }

        bool isValid = IsPositionValid(obj.transform.position);
        UpdateObjectVisuals(isValid);

        bool isRotatable = ((1 << obj.layer) & rotatableLayer) != 0;
        UpdateUIState(isRotatable);
        if (isRotatable)
        {
            CalculateCurrentRotationIndex();
            UpdateRotationVisual();
        }
        if (ToolUIManager.Instance != null)
{
    ToolUIManager.Instance.ShowBubbleForTool(obj, isRotatable);
}

        Debug.Log($"Selected: {obj.name}, valid: {isValid}, rotatable: {isRotatable}");
    }

    private void ResetSelection()
    {
        if (ToolUIManager.Instance != null)
{
    ToolUIManager.Instance.HideAllBubbles();
}
        RestoreOriginalMaterials();
        
        UpdateUIState(false);
        
        selectedObject = null;
        objectRenderers = null;
        isPermanentlySelected = false;
        isDragging = false;
        
        Debug.Log("Selection reset complete");
    }
    #endregion
}