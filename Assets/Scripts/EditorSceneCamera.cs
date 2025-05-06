using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FixedSceneCamera : MonoBehaviour
{
    [Header("Настройки")]
    public float zoomSpeed = 10f;
    public float minZoom = 5f;
    public float maxZoom = 50f;
    public float panSpeed = 5f;
    public KeyCode switchViewKey = KeyCode.V;

    [Header("Состояние")]
    [SerializeField] private bool _isActive = true;

    private Vector3 _originalPosition;
    private Vector2 _lastPanPosition;
    private bool _isPanning;
    private float _currentZoom;

    void OnEnable()
    {
        #if UNITY_EDITOR
        SceneView.duringSceneGui += DuringSceneGUI;
        #endif
        
        _originalPosition = transform.position;
        _currentZoom = -transform.localPosition.z;
    }

    void OnDisable()
    {
        #if UNITY_EDITOR
        SceneView.duringSceneGui -= DuringSceneGUI;
        #endif
    }

    #if UNITY_EDITOR
    void DuringSceneGUI(SceneView sceneView)
    {
        if (!_isActive) return;

        Event e = Event.current;

        // Переключение вида
        if (e.type == EventType.KeyDown && e.keyCode == switchViewKey)
        {
            sceneView.AlignViewToObject(transform);
            sceneView.Repaint();
            return;
        }

        // Зум (только по Z)
        if (e.type == EventType.ScrollWheel)
        {
            _currentZoom = Mathf.Clamp(
                _currentZoom + e.delta.y * zoomSpeed,
                minZoom,
                maxZoom
            );
            
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                transform.localPosition.y,
                -_currentZoom
            );
            
            e.Use();
        }

        // Панорамирование (только по X/Y)
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            _lastPanPosition = e.mousePosition;
            _isPanning = true;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 1)
        {
            _isPanning = false;
            e.Use();
        }

        if (_isPanning && e.type == EventType.MouseDrag)
        {
            Vector2 delta = e.mousePosition - _lastPanPosition;
            transform.Translate(
                new Vector3(-delta.x * panSpeed * 0.01f, 
                delta.y * panSpeed * 0.01f, 
                0),
                Space.Self
            );
            _lastPanPosition = e.mousePosition;
            e.Use();
        }
    }
    #endif

    #if UNITY_EDITOR
    [CustomEditor(typeof(FixedSceneCamera))]
    public class FixedSceneCameraEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            if (GUILayout.Button("Смотреть из камеры"))
            {
                SceneView.lastActiveSceneView.AlignViewToObject(((FixedSceneCamera)target).transform);
                SceneView.lastActiveSceneView.Repaint();
            }
        }
    }
    #endif
}