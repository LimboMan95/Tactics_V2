using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TileSnapper : MonoBehaviour
{
    [Header("Настройки")]
    public Vector2Int tileSize = Vector2Int.one;
    public bool centerOnTile = true;
    public float yOffset = 0;
    public bool snapToGround = true;
    public bool lockYPosition = false;
    [Tooltip("Автоматически поднимает объект, чтобы он не утопало в земле")]
    public bool autoAdjustHeight = true;

    [Header("Debug")]
    public Color gizmoColor = new Color(1, 0.5f, 0, 0.5f);

    private Vector3 lastPosition;
    private bool wasInPlayMode = false;

    void Update()
    {
        if (!Application.isPlaying && transform.position != lastPosition)
        {
            SnapToGrid();
            lastPosition = transform.position;
        }
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            wasInPlayMode = true;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (wasInPlayMode)
            {
                SnapToGrid();
                wasInPlayMode = false;
            }
        }
    }
#endif

    public void SnapToGrid()
    {
        if (Application.isPlaying) return;
        
        if (GridGenerator.Instance == null)
        {
            GridGenerator.Instance = FindAnyObjectByType<GridGenerator>();
            if (GridGenerator.Instance == null)
            {
                Debug.LogError("Add GridGenerator to scene!", this);
                return;
            }
        }

        float spacing = GridGenerator.Instance.tileSpacing;
        Vector3 pos = transform.position;

        if (lockYPosition) pos.y = yOffset;

        // Расчет позиции в сетке тайлов
        int gridX = Mathf.RoundToInt(pos.x / spacing);
        int gridZ = Mathf.RoundToInt(pos.z / spacing);
        
        // Расчет целевой позиции в мировых координатах
        Vector3 targetPos;
        if (centerOnTile)
        {
            targetPos = new Vector3(gridX * spacing, pos.y, gridZ * spacing);
        }
        else
        {
            // Корректное смещение для объектов, занимающих несколько тайлов.
            // Мы вычисляем центр занимаемой области, а не просто привязываемся к углу.
            targetPos = new Vector3(
                (gridX * spacing) + (tileSize.x - 1) * spacing * 0.5f,
                pos.y,
                (gridZ * spacing) + (tileSize.y - 1) * spacing * 0.5f
            );
        }
        
        pos.x = targetPos.x;
        pos.z = targetPos.z;

        // Обработка высоты
        if (snapToGround && !lockYPosition)
        {
            Vector3 raycastOrigin = new Vector3(pos.x, 100, pos.z);
            
            if (Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, 200))
            {
                if (autoAdjustHeight)
                {
                    Renderer rend = GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        float modelHeight = rend.bounds.size.y;
                        pos.y = hit.point.y + (modelHeight * 0.5f) + yOffset;
                    }
                    else
                    {
                        pos.y = hit.point.y + yOffset;
                    }
                }
                else
                {
                    pos.y = hit.point.y + yOffset;
                }
            }
            else
            {
                pos.y = yOffset;
            }
        }
        else
        {
            pos.y = yOffset;
        }

        transform.position = pos;
        lastPosition = pos;
    }

    void OnDrawGizmosSelected()
    {
        if (GridGenerator.Instance == null) return;
        
        float spacing = GridGenerator.Instance.tileSpacing;
        
        // Рисуем основной оранжевый гизмо
        Gizmos.color = gizmoColor;
        Vector3 size = new Vector3(
            tileSize.x * spacing,
            0.1f,
            tileSize.y * spacing
        );
        
        Vector3 center;
        
        if (centerOnTile)
        {
            center = transform.position;
        }
        else
        {
            center = transform.position; // Центр гизмо совпадает с позицией объекта
        }
        
        Gizmos.DrawCube(center + new Vector3(0, yOffset + 0.05f, 0), size);
        
        // Рисуем границы каждого тайла
        Gizmos.color = Color.red;
        for (int x = 0; x < tileSize.x; x++)
        {
            for (int z = 0; z < tileSize.y; z++)
            {
                Vector3 tileCenter;
                
                // Пересчитываем позицию каждого тайла относительно центра объекта
                tileCenter = transform.position + new Vector3(
                    (x - (tileSize.x - 1) * 0.5f) * spacing,
                    yOffset + 0.06f,
                    (z - (tileSize.y - 1) * 0.5f) * spacing
                );
                
                Gizmos.DrawWireCube(tileCenter, new Vector3(spacing * 0.95f, 0.02f, spacing * 0.95f));
            }
        }
        
        // Рисуем линию до земли для отладки
        if (snapToGround && !lockYPosition)
        {
            Vector3 raycastOrigin = new Vector3(transform.position.x, 100, transform.position.z);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(raycastOrigin, raycastOrigin + Vector3.down * 200);
            
            if (Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, 200))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(hit.point, 0.2f);
            }
        }
        
        // Рисуем точку центра привязки
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 0.1f);
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Snap Selected %#s")]
    static void SnapSelected()
    {
        foreach (var obj in Selection.gameObjects)
            if (obj.TryGetComponent<TileSnapper>(out var snapper))
                snapper.SnapToGrid();
    }
#endif
}