using UnityEngine;

public class LevelEditorCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public float zoomSpeed = 10f;
    public float minZoom = 5f;
    public float maxZoom = 50f;
    public float panSpeed = 5f;

    [Header("Camera Switching")]
    public Camera gameCamera;
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Debug")]
    [SerializeField] private bool editorCameraActive = true;

    private Vector3 lastPanPosition;
    private bool isPanning;

    void Start()
    {
        // Инициализация состояний камер
        if (gameCamera != null)
        {
            GetComponent<Camera>().enabled = true;
            gameCamera.enabled = false;
        }
        else
        {
            Debug.LogError("Game Camera не назначена!");
        }
    }

    void Update()
    {
        // Переключение камер
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCameras();
            Debug.Log($"Камеры переключены. Editor активна: {GetComponent<Camera>().enabled}");
        }

        // Управление только для активной камеры
        if (GetComponent<Camera>().enabled)
        {
            HandleZoomAndPan();
        }
    }

    private void ToggleCameras()
    {
        if (gameCamera == null) return;

        editorCameraActive = !editorCameraActive;
        GetComponent<Camera>().enabled = editorCameraActive;
        gameCamera.enabled = !editorCameraActive;
    }

    private void HandleZoomAndPan()
    {
        // Зум
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Zoom(scroll * zoomSpeed);
        }

        // Панорамирование
        if (Input.GetMouseButtonDown(1))
        {
            lastPanPosition = Input.mousePosition;
            isPanning = true;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isPanning = false;
        }

        if (isPanning)
        {
            PanCamera();
        }
    }

    private void Zoom(float delta)
    {
        transform.position += transform.forward * delta;
        transform.position = new Vector3(
            transform.position.x,
            Mathf.Clamp(transform.position.y, minZoom, maxZoom),
            transform.position.z
        );
    }

    private void PanCamera()
    {
        Vector3 mouseDelta = Input.mousePosition - lastPanPosition;
        Vector3 panTranslation = new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * panSpeed * Time.deltaTime;
        transform.Translate(panTranslation, Space.World);
        lastPanPosition = Input.mousePosition;
    }
}