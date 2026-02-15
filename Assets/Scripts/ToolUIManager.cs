using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ToolUIManager : MonoBehaviour
{
    public Canvas bubbleCanvas;
    public GameObject bubblePrefab;
    public float bubbleHeight = 2f;
    
    private Dictionary<GameObject, GameObject> activeBubbles = new Dictionary<GameObject, GameObject>();
    private GridObjectMover mover;
    private Camera mainCamera;
    
    public static ToolUIManager Instance { get; private set; }
    
    void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        mover = FindObjectOfType<GridObjectMover>();
    }
    
    public void ShowBubbleForTool(GameObject tool, bool isRotatable)
    {
        if (activeBubbles.ContainsKey(tool)) return;
        
        GameObject bubble = Instantiate(bubblePrefab, bubbleCanvas.transform);
        bubble.name = $"Bubble_{tool.name}";
        
        Vector3 worldPos = tool.transform.position + Vector3.up * bubbleHeight;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        bubble.GetComponent<RectTransform>().position = screenPos;
        
        Button[] buttons = bubble.GetComponentsInChildren<Button>(true);
        
        foreach (Button btn in buttons)
        {
            btn.onClick.RemoveAllListeners();
            
            if (btn.name.Contains("Left"))
            {
                btn.onClick.AddListener(() => mover?.RotateSelectedObjectLeft());
            }
            else if (btn.name.Contains("Right"))
            {
                btn.onClick.AddListener(() => mover?.RotateSelectedObjectRight());
            }
            
            btn.gameObject.SetActive(isRotatable);
        }
        
        activeBubbles[tool] = bubble;
    }
    
    public void HideAllBubbles()
    {
        foreach (var bubble in activeBubbles.Values)
        {
            if (bubble != null) Destroy(bubble);
        }
        activeBubbles.Clear();
    }
    
    void Update()
    {
        foreach (var pair in activeBubbles)
        {
            if (pair.Key != null && pair.Value != null)
            {
                Vector3 worldPos = pair.Key.transform.position + Vector3.up * bubbleHeight;
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                pair.Value.GetComponent<RectTransform>().position = screenPos;
            }
        }
    }
}