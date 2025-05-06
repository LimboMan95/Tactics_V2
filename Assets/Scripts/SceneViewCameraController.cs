using UnityEditor;
using UnityEngine;

public class SceneViewCameraController : MonoBehaviour
{
    [Header("Настройки")]
    public float zoomSpeed = 10f;
    public float minZoom = 5f;
    public float maxZoom = 50f;
    public float panSpeed = 5f;

    private Vector3 lastPanPosition;
    private bool isPanning;

    void OnEnable()
    {
        // Автоматически выравниваем камеру редактора под текущий вид
        AlignWithSceneView();
        Debug.Log("Редакторская камера активирована!");
    }

    // Выравнивает камеру под текущий вид SceneView
    private void AlignWithSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            transform.position = sceneView.camera.transform.position;
            transform.rotation = sceneView.camera.transform.rotation;
        }
    }

    void Update()
    {
        if (!isActiveAndEnabled) return;

        // Зум
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Zoom(scroll * zoomSpeed);
        }

        // Панорамирование (ПКМ)
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
        transform.Translate(Vector3.forward * delta, Space.Self);
        float currentZoom = transform.position.y;
        transform.position = new Vector3(
            transform.position.x,
            Mathf.Clamp(currentZoom, minZoom, maxZoom),
            transform.position.z
        );
    }

    private void PanCamera()
    {
        Vector3 delta = Input.mousePosition - lastPanPosition;
        Vector3 move = new Vector3(-delta.x, 0, -delta.y) * panSpeed * 0.01f;
        transform.Translate(move, Space.Self);
        lastPanPosition = Input.mousePosition;
    }
}