using UnityEngine;

public class DraggableObject : MonoBehaviour
{
    [Header("Settings")]
    public string allowedLayer = "Tools"; // Название слоя для перетаскивания
    
    private bool isDragging;
    private Vector3 offset;
    private float zCoord;
    private int toolsLayerMask;

    void Start()
    {
        // Получаем маску слоя по имени
        toolsLayerMask = LayerMask.NameToLayer(allowedLayer);
    }

    void OnMouseDown()
    {
        // Проверяем, что объект на нужном слое и компонент включен
        if (!enabled || gameObject.layer != toolsLayerMask) return;
        
        isDragging = true;
        zCoord = Camera.main.WorldToScreenPoint(transform.position).z;
        offset = transform.position - GetMouseWorldPos();
    }

    void OnMouseUp()
    {
        isDragging = false;
    }

    void Update()
    {
        if (isDragging && enabled)
        {
            transform.position = GetMouseWorldPos() + offset;
        }
    }

    Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = zCoord;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}