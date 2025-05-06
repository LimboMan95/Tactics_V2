using UnityEngine;
using System.Collections.Generic;

public class GamePhaseManager : MonoBehaviour
{
    public enum GamePhase
    {
        Planning,
        Execution
    }

    [Header("Settings")]
    public KeyCode playButton = KeyCode.Space;
    public KeyCode stopButton = KeyCode.Escape;
    public string toolsLayerName = "Tools"; // Название слоя для перемещаемых объектов

    [Header("References")]
    public DickControlledCube cubeController;
    public CameraController cameraController;

    private GamePhase currentPhase = GamePhase.Planning;
    private List<Vector3> initialPositions = new List<Vector3>();
    private List<Transform> movableObjects = new List<Transform>();
    private int toolsLayerMask;

    void Start()
{
    // Инициализируем куб перед использованием
    if (cubeController != null)
    {
        // Принудительная инициализация куба
        cubeController.Initialize(); 
        
        // Сразу ставим в режим планирования
        cubeController.enabled = false;
        cubeController.ResetToInitialState(true);
    }
    
    FindAllMovableObjects();
}

    void Update()
    {
        if (Input.GetKeyDown(playButton) && currentPhase == GamePhase.Planning)
        {
            StartExecutionPhase();
        }
        else if (Input.GetKeyDown(stopButton) && currentPhase == GamePhase.Execution)
        {
            StopExecutionPhase();
        }
    }

   void FindAllMovableObjects()
{
    movableObjects.Clear();
    initialPositions.Clear();

    // Используем новый метод FindObjectsByType
    GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    
    foreach (GameObject obj in allObjects)
    {
        // Проверяем, что объект на нужном слое
        if (obj != null && ((1 << obj.layer) & toolsLayerMask) != 0)
        {
            movableObjects.Add(obj.transform);
            initialPositions.Add(obj.transform.position);
            
            // Добавляем компонент DraggableObject если его нет
            DraggableObject draggable = obj.GetComponent<DraggableObject>();
            if (draggable == null)
            {
                draggable = obj.AddComponent<DraggableObject>();
            }
            draggable.enabled = false;
        }
    }
}

 void SetPlanningPhase()
{
    currentPhase = GamePhase.Planning;
    
    if (cubeController != null)
    {
        // Сбрасываем в kinematic режим
        cubeController.enabled = false;
        cubeController.ResetToInitialState(true); // Явно указываем kinematic
        
        // Больше не нужно управлять Rigidbody здесь
    }
    
    // Включаем перетаскивание
    foreach (Transform obj in movableObjects)
    {
        if (obj != null)
        {
            var draggable = obj.GetComponent<DraggableObject>();
            if (draggable != null) draggable.enabled = true;
        }
    }
    
    if (cameraController != null)
    {
        cameraController.SwitchToPlanningView();
    }
}

void StartExecutionPhase()
{
    currentPhase = GamePhase.Execution;
    
    if (cubeController != null)
    {
        // Сбрасываем в динамический режим
        cubeController.ResetToInitialState(false); // Не kinematic
        cubeController.enabled = true;
    }
    
    // Отключаем перетаскивание
    foreach (Transform obj in movableObjects)
    {
        if (obj != null)
        {
            var draggable = obj.GetComponent<DraggableObject>();
            if (draggable != null) draggable.enabled = false;
        }
    }
    
    if (cameraController != null)
    {
        cameraController.SwitchToGameplayView();
    }
}

void StopExecutionPhase()
{
    // Сначала сброс объектов
    for (int i = 0; i < movableObjects.Count; i++)
    {
        if (movableObjects[i] != null && i < initialPositions.Count)
        {
            movableObjects[i].position = initialPositions[i];
        }
    }
    
    // Затем сброс куба
    if (cubeController != null)
    {
        cubeController.ResetToInitialState();
    }
    
    SetPlanningPhase();
}
}