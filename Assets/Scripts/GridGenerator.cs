using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GridGenerator : MonoBehaviour
{
    public static GridGenerator Instance;

    [Header("Настройки")]
    public GameObject tilePrefab;
    [Min(1)] public int gridSize = 10;
    [Min(0.1f)] public float tileSpacing = 1.0f;
    public bool centerGrid = true;

    [Header("Визуализация")]
    public bool drawInEditor = true;
    public Color gridColor = Color.cyan;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void GenerateGrid()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediateChildren();
        }
        else
        #endif
        {
            DestroyChildren();
        }

        if (!tilePrefab) return;

        Vector3 offset = centerGrid ? new Vector3(
            -gridSize * tileSpacing * 0.5f,
            0,
            -gridSize * tileSpacing * 0.5f
        ) : Vector3.zero;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Instantiate(
                    tilePrefab,
                    new Vector3(x * tileSpacing, 0, z * tileSpacing) + offset,
                    Quaternion.identity,
                    transform
                );
            }
        }
    }

    void DestroyChildren()
    {
        while (transform.childCount > 0)
        {
            Destroy(transform.GetChild(0).gameObject);
        }
    }

    #if UNITY_EDITOR
    void DestroyImmediateChildren()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
    #endif

    #if UNITY_EDITOR
    [CustomEditor(typeof(GridGenerator))]
    class Editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Generate Grid")) ((GridGenerator)target).GenerateGrid();
        }
    }
    #endif
}