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
    public bool autoAdjustHeight = true; // Новый флаг!

    [Header("Debug")]
    public Color gizmoColor = new Color(1, 0.5f, 0, 0.5f);

    private Vector3 lastPosition;

    void Update()
    {
        if (!Application.isPlaying && transform.position != lastPosition)
        {
            SnapToGrid();
            lastPosition = transform.position;
        }
    }

    public void SnapToGrid()
    {
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

        // Сброс Y, если включена фиксация
        if (lockYPosition) pos.y = yOffset;

        // Выравнивание по сетке (XZ)
        pos.x = Mathf.Round(pos.x / spacing) * spacing;
        pos.z = Mathf.Round(pos.z / spacing) * spacing;

        // Центрирование
        if (centerOnTile)
        {
            pos.x -= (tileSize.x - 1) * spacing * 0.5f;
            pos.z -= (tileSize.y - 1) * spacing * 0.5f;
        }

        // Обработка высоты
        if (snapToGround && !lockYPosition)
        {
            if (Physics.Raycast(new Vector3(pos.x, 100, pos.z), Vector3.down, out RaycastHit hit, 200))
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
    }

    void OnDrawGizmosSelected()
    {
        if (GridGenerator.Instance == null) return;
        
        Gizmos.color = gizmoColor;
        Vector3 size = new Vector3(
            tileSize.x * GridGenerator.Instance.tileSpacing,
            0.1f,
            tileSize.y * GridGenerator.Instance.tileSpacing
        );
        Gizmos.DrawCube(transform.position + new Vector3(0, yOffset, 0), size);
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