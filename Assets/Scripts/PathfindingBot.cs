using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PathfindingBot : MonoBehaviour
{
    [Header("Bot Settings")]
    public DickControlledCube cubeController;
    public GridObjectMover gridMover;
    public float searchTimeout = 5f; // Максимальное время на поиск пути
    public float setupWaitTime = 0.1f; // Задержка между расстановкой тулов
    public float movementWaitTime = 0.5f; // Задержка между действиями бота

    // НАСТРОЙКИ СЛОЁВ И ТЕГОВ
    [Header("Layers and Tags")]
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public LayerMask finishLayer;
    public LayerMask toolLayer; // Слой для перемещаемых инструментов
    public string directionTileTag; // Тег для поворотного тайла
    public string jumpTileTag; // Тег для прыжкового тайла

    private List<System.Action> finalActions;
    private bool isExecutingPath = false;
    private bool isPathFound = false;
     [Header("AI Settings")]
    public HeuristicType heuristicType = HeuristicType.DistanceAndTools;
    public float distanceWeight = 10f;
    public float toolsWeight = 50f;
    public float directionWeight = 20f;
    
    public enum HeuristicType { DistanceOnly, DistanceAndTools, Advanced }
     private List<ToolState> initialToolStates; // <-- ДОБАВЬТЕ ЭТУ СТРОКУ
    
    // Приоритетная очередь
    private PriorityQueue<Node> priorityQueue;

    // Класс для хранения состояния тула (позиция и вращение)
    [System.Serializable]
    public class ToolState
    {
        public GameObject toolObject;
        public Vector3 position;
        public Quaternion rotation;

        public ToolState Clone()
        {
            return new ToolState
            {
                toolObject = this.toolObject,
                position = this.position,
                rotation = this.rotation
            };
        }
    }

    // Класс для хранения полного состояния уровня (куб + все тулы)
    public class Node
    {
        public Vector3 cubePosition;
        public Vector3 cubeDirection;
        public List<ToolState> levelState;
        public Node parent;
        public int cost = 0;
         public int actionCost = 0; // ← ДОБАВЬ ЭТО
    }

    void Start()
    {
        if (cubeController == null)
        {
            cubeController = GetComponent<DickControlledCube>();
        }
        if (gridMover == null)
        {
            gridMover = FindObjectOfType<GridObjectMover>();
        }
    }

    public void StartBot()
    {
        if (isExecutingPath) return;
        StopAllCoroutines();
        isExecutingPath = false;
        isPathFound = false;
        finalActions = null;
        Debug.Log("Bot: Starting pathfinding...");
        StartCoroutine(FindPathAndExecute());
    }

    private IEnumerator FindPathAndExecute()
    {
        // СОХРАНЯЕМ начальное состояние инструментов ПЕРЕД началом поиска
    initialToolStates = GetCurrentToolStates(); // <-- ДОБАВЬТЕ ЭТУ СТРОКУ
        yield return StartCoroutine(BFS_Pathfinding());

        if (isPathFound && finalActions != null && finalActions.Count > 0)
        {
            Debug.Log($"Bot: Path found. Total cost: {finalActions.Count}. Executing...");
            yield return StartCoroutine(ExecutePath());
            Debug.Log("Bot: Execution complete.");
        }
        else
        {
            Debug.LogWarning("Bot: No valid path found.");
        }
    }

    private IEnumerator BFS_Pathfinding()
{
    isPathFound = false;
     priorityQueue = new PriorityQueue<Node>(); // ← УБРАТЬ (CompareNodes)
    var visited = new HashSet<string>();
    float startTime = Time.time; // ← ДОБАВЛЕНО
    
    Node startNode = CreateStartNode();
    startNode.cost = CalculateHeuristic(startNode);
    
    priorityQueue.Enqueue(startNode, startNode.cost);
    visited.Add(GetStateKey(startNode));
    
    while (priorityQueue.Count > 0)
    {
        if (Time.time - startTime > searchTimeout) 
        {
            Debug.LogWarning("Bot: Pathfinding timeout reached.");
            yield break;
        }
        
        Node currentNode = priorityQueue.Dequeue();
        Debug.Log($"Processing node at {currentNode.cubePosition}. " +
                 $"Tools: {currentNode.levelState.Count}, " +
                 $"Unused: {CountUnusedTools(currentNode.levelState)}, " +
                 $"Cost: {currentNode.cost}");
        
        if (IsFinishTrigger(currentNode.cubePosition))
        {
            ReconstructActions(currentNode);
            isPathFound = true;
            yield break;
        }
        
        // СНАЧАЛА инструменты (ВЫСОКИЙ ПРИОРИТЕТ)
        var toolActions = new List<Node>(GenerateToolActions(currentNode));
        foreach (var node in toolActions)
        {
            string stateKey = GetStateKey(node);
            if (!visited.Contains(stateKey))
            {
                node.cost = currentNode.cost + 1 + CalculateHeuristic(node);
                priorityQueue.Enqueue(node, node.cost);
                visited.Add(stateKey);
                Debug.Log($"Added tool action. New cost: {node.cost}");
            }
        }
        
        // ПОТОМ движение (НИЗКИЙ ПРИОРИТЕТ)
        var moveActions = new List<Node>(GenerateCubeMoves(currentNode));
        foreach (var node in moveActions)
        {
            string stateKey = GetStateKey(node);
            if (!visited.Contains(stateKey))
            {
                node.cost = currentNode.cost + 10 + CalculateHeuristic(node); // ↑ Высокая стоимость движения
                priorityQueue.Enqueue(node, node.cost);
                visited.Add(stateKey);
            }
        }
        
        yield return null;
    }
}

private Node CreateStartNode()
{
    return new Node
    {
        cubePosition = gridMover.GetSnappedPosition(cubeController.InitialPosition),
        cubeDirection = cubeController.InitialDirection,
        levelState = GetCurrentToolStates(),
        parent = null,
        cost = 0
    };
}

private IEnumerable<Node> GenerateSpecialActions(Node currentNode)
{
    // Пока оставляем пустым, можно добавить позже
    return new List<Node>();
}


    private int CompareNodes(Node a, Node b)
    {
        return a.cost.CompareTo(b.cost); // Меньшая стоимость = высший приоритет
    }
    
   private int CalculateHeuristic(Node node)
{
    float heuristic = 0;
    
    switch (heuristicType)
    {
        case HeuristicType.DistanceOnly:
            heuristic = Vector3.Distance(node.cubePosition, FindFinishPosition()) * distanceWeight;
            break;
            
        case HeuristicType.DistanceAndTools:
            heuristic = Vector3.Distance(node.cubePosition, FindFinishPosition()) * distanceWeight +
                       CountUnusedTools(node.levelState) * toolsWeight * 100;
            break;
            
        case HeuristicType.Advanced:
            Vector3 toFinish = (FindFinishPosition() - node.cubePosition).normalized;
            float directionMatch = Vector3.Dot(node.cubeDirection, toFinish);
            
            heuristic = Vector3.Distance(node.cubePosition, FindFinishPosition()) * distanceWeight +
                       CountUnusedTools(node.levelState) * toolsWeight * 1000 -
                       directionMatch * directionWeight;
            break;
    }
    
    Debug.Log($"Heuristic for {node.cubePosition}: {heuristic} (unused tools: {CountUnusedTools(node.levelState)})");
    return Mathf.RoundToInt(heuristic); // ← Преобразование в конце
}

     private IEnumerable<Node> GenerateAllPossibleActions(Node currentNode)
    {
        var actions = new List<Node>();
        
        // 1. Действия с инструментами (ВЫСОКИЙ ПРИОРИТЕТ)
        actions.AddRange(GenerateToolActions(currentNode));
        
        // 2. Движения куба (СРЕДНИЙ ПРИОРИТЕТ)
        actions.AddRange(GenerateCubeMoves(currentNode));
        
        // 3. Специальные действия (ПРЫЖКИ и т.д.)
        actions.AddRange(GenerateSpecialActions(currentNode));
        
        return actions;
    }

// Вспомогательные методы
private Vector3 FindFinishPosition()
{
    // Находим позицию финиша на сцене
    GameObject finish = GameObject.FindGameObjectWithTag("Finish");
    return finish != null ? finish.transform.position : Vector3.zero;
}

private int CountUnusedTools(List<ToolState> currentToolStates)
{
    // Если начальное состояние еще не сохранено, возвращаем 0
    if (initialToolStates == null || initialToolStates.Count == 0)
        return 0;

    int count = 0;
    
    foreach (var currentTool in currentToolStates)
    {
        // Находим соответствующий инструмент в начальном состоянии по имени объекта
        var initialTool = initialToolStates.Find(t => t.toolObject.name == currentTool.toolObject.name);
        
        if (initialTool != null)
        {
            // Сравниваем вращение. Если оно отличается от начального - инструмент был использован.
            // Сравниваем углы по оси Y, так как мы вращаем только вокруг нее.
            bool isRotationChanged = !Mathf.Approximately(
                currentTool.rotation.eulerAngles.y, 
                initialTool.rotation.eulerAngles.y
            );
            
            // Если вращение НЕ изменилось (осталось как вначале) - инструмент НЕ использован
            if (!isRotationChanged) 
            {
                count++;
            }
        }
    }
    return count;
}

    private IEnumerable<Node> GenerateToolActions(Node currentNode)
{
    var actions = new List<Node>();
    foreach (var tool in currentNode.levelState)
    {
       if (((1 << tool.toolObject.layer) & gridMover.rotatableLayer) != 0)
{
    // 1. Поворот на 90 градусов (по часовой)
    Node rotateNodeCW = new Node
    {
        cubePosition = currentNode.cubePosition,
        cubeDirection = currentNode.cubeDirection,
        levelState = CloneToolStates(currentNode.levelState),
        parent = currentNode,
        cost = currentNode.cost + 1,
        actionCost = 1
    };
    var toolToRotateCW = rotateNodeCW.levelState.Find(t => t.toolObject == tool.toolObject);
    toolToRotateCW.rotation *= Quaternion.Euler(0, 90, 0);
    actions.Add(rotateNodeCW);

    // 2. Поворот на -90 градусов (против часовой)
    Node rotateNodeCCW = new Node
    {
        cubePosition = currentNode.cubePosition,
        cubeDirection = currentNode.cubeDirection,
        levelState = CloneToolStates(currentNode.levelState),
        parent = currentNode,
        cost = currentNode.cost + 1,
        actionCost = 1
    };
    var toolToRotateCCW = rotateNodeCCW.levelState.Find(t => t.toolObject == tool.toolObject);
    toolToRotateCCW.rotation *= Quaternion.Euler(0, -90, 0);
    actions.Add(rotateNodeCCW);
}
    }
    return actions;
}

    private IEnumerable<Node> GenerateCubeMoves(Node currentNode)
    {
        var moves = new List<Node>();
        RaycastHit hit;
       // 1. Движение вперед на одну клетку
    Vector3 nextPosition = gridMover.GetSnappedPosition(currentNode.cubePosition + currentNode.cubeDirection * cubeController.tileSize);
    
    Debug.Log($"Bot: Checking move from {currentNode.cubePosition} to {nextPosition}");
    
    bool isMoveValid = IsMoveValid(nextPosition);
    
    if (isMoveValid)
    {
        Debug.Log($"Bot: Forward move to {nextPosition} is VALID");
        Node moveNode = new Node
        {
            cubePosition = nextPosition,
            cubeDirection = currentNode.cubeDirection,
            levelState = currentNode.levelState,
            parent = currentNode,
            cost = currentNode.cost
        };
        moves.Add(moveNode);
    }
    else
    {
        Debug.Log($"Bot: Forward move to {nextPosition} is INVALID");
    }

        // 2. Проверка на поворот (направляющая плитка)
        if (Physics.Raycast(currentNode.cubePosition + Vector3.up * 0.1f, Vector3.down, out hit, 1f, groundLayer))
        {
            if (hit.collider.CompareTag(directionTileTag))
            {
                Vector3 newDirection = hit.transform.forward;
                if (Vector3.Angle(currentNode.cubeDirection, newDirection) > 5f)
                {
                     Node rotateCubeNode = new Node { 
                         cubePosition = currentNode.cubePosition, 
                         cubeDirection = newDirection, 
                         levelState = currentNode.levelState, 
                         parent = currentNode, 
                         cost = currentNode.cost 
                     };
                     moves.Add(rotateCubeNode);
                }
            }
        }
        
        // 3. Проверка на прыжок (прыжковая плитка)
        if (Physics.Raycast(currentNode.cubePosition + Vector3.up * 0.1f, Vector3.down, out hit, 1f, groundLayer))
        {
            if (hit.collider.CompareTag(jumpTileTag))
            {
                Vector3 jumpTarget = gridMover.GetSnappedPosition(currentNode.cubePosition + currentNode.cubeDirection * cubeController.jumpDistance);
                if (IsJumpValid(jumpTarget))
                {
                    Node jumpNode = new Node { 
                        cubePosition = jumpTarget, 
                        cubeDirection = currentNode.cubeDirection, 
                        levelState = currentNode.levelState, 
                        parent = currentNode, 
                        cost = currentNode.cost 
                    };
                    moves.Add(jumpNode);
                }
            }
        }
        return moves;
    }
    
    private IEnumerator ExecutePath()
    {
        Debug.Log("Bot: Setting up the level. This will cost you " + finalActions.FindAll(a => a != null && a.Method.Name != "MoveNext").Count + " points.");
        gridMover.ForceEnableEditMode();
        
        foreach (var action in finalActions)
        {
            action.Invoke();
            yield return new WaitForSeconds(setupWaitTime);
        }
        
        gridMover.ForceDisableEditMode();
        
        cubeController.movementEnabled = true;
        cubeController.RB.MovePosition(cubeController.InitialPosition);
        cubeController.RB.MoveRotation(Quaternion.LookRotation(cubeController.InitialDirection));

        while(!IsFinishTrigger(transform.position))
        {
            yield return new WaitForFixedUpdate();
        }
        cubeController.movementEnabled = false;
        cubeController.RB.linearVelocity = Vector3.zero;
    }
    
    private void ReconstructActions(Node endNode)
    {
        finalActions = new List<System.Action>();
        Node current = endNode;
        while (current.parent != null)
        {
            if (!AreToolStatesEqual(current.levelState, current.parent.levelState))
            {
                var diff = GetStateDifference(current.levelState, current.parent.levelState);
                if (diff != null)
                {
                    finalActions.Add(() => {
                        diff.toolObject.transform.position = diff.position;
                        diff.toolObject.transform.rotation = diff.rotation;
                    });
                }
            }
            else if (current.cubePosition != current.parent.cubePosition || current.cubeDirection != current.parent.cubeDirection)
            {
                finalActions.Add(() => {
                    cubeController.ExecuteBotMove(current.cubePosition, current.cubeDirection);
                });
            }

            current = current.parent;
        }
        finalActions.Reverse();
    }

    private bool AreToolStatesEqual(List<ToolState> state1, List<ToolState> state2)
    {
        if (state1.Count != state2.Count) return false;
        for (int i = 0; i < state1.Count; i++)
        {
            if (state1[i].position != state2[i].position || state1[i].rotation != state2[i].rotation)
            {
                return false;
            }
        }
        return true;
    }
    
    private ToolState GetStateDifference(List<ToolState> state1, List<ToolState> state2)
    {
        for (int i = 0; i < state1.Count; i++)
        {
            if (state1[i].position != state2[i].position || state1[i].rotation != state2[i].rotation)
            {
                return state1[i];
            }
        }
        return null;
    }

    private List<ToolState> GetCurrentToolStates()
    {
        var states = new List<ToolState>();
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (((1 << obj.layer) & toolLayer) != 0)
            {
                states.Add(new ToolState
                {
                    toolObject = obj,
                    position = gridMover.GetSnappedPosition(obj.transform.position),
                    rotation = obj.transform.rotation
                });
            }
        }
        return states;
    }
    
    private List<ToolState> CloneToolStates(List<ToolState> original)
    {
        var clone = new List<ToolState>();
        foreach (var state in original)
        {
            clone.Add(state.Clone());
        }
        return clone;
    }

    private string GetStateKey(Node node)
    {
        string key = $"{node.cubePosition.x},{node.cubePosition.z},{node.cubeDirection.x},{node.cubeDirection.z}";
        foreach (var tool in node.levelState)
        {
            key += $":{tool.toolObject.name}:{tool.position.x},{tool.position.z}:{tool.rotation.eulerAngles.y}";
        }
        return key;
    }

    private bool IsMoveValid(Vector3 pos)
    {
    // 1. Проверяем, что под клеткой есть земля
    bool hasGround = Physics.Raycast(pos, Vector3.down, 1f, groundLayer);
    
    // 2. Проверяем, что на клетке нет препятствий
    bool hasObstacle = Physics.Raycast(pos, Vector3.down, 1f, obstacleLayer);
    
    Debug.Log($"Bot: Check move at {pos}: ground={hasGround}, obstacle={hasObstacle}");
    
    return hasGround && !hasObstacle;
    }
    
    private bool IsJumpValid(Vector3 pos)
    {
       // 1. Проверяем, что под целевой клеткой есть земля
    bool hasGround = Physics.Raycast(pos, Vector3.down, 1f, groundLayer);
    
    // 2. Проверяем, что на целевой клетке нет препятствий
    bool hasObstacle = Physics.Raycast(pos, Vector3.down, 1f, obstacleLayer);
    
    Debug.Log($"Bot: Check jump to {pos}: ground={hasGround}, obstacle={hasObstacle}");
    
    return hasGround && !hasObstacle;
    }

    private bool IsFinishTrigger(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, 0.1f, finishLayer);
    bool isFinish = colliders.Length > 0;
    if (isFinish) Debug.Log($"Bot: Found finish at {pos}!");
    return isFinish;
    }
    
    private Vector3 GetSnappedPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / cubeController.tileSize) * cubeController.tileSize,
            position.y,
            Mathf.Round(position.z / cubeController.tileSize) * cubeController.tileSize
        );
    }
}