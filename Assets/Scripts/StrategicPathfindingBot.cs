using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class StrategicPathfindingBot : MonoBehaviour
{
    [Header("References")]
    public DickControlledCube cubeController;
    public GridObjectMover gridMover;
    
    [Header("Settings")]
    public float setupWaitTime = 0.1f;
    public float movementWaitTime = 0.3f;
    public string directionTileTag = "DirectionTile";
    public string jumpTileTag = "JumpTile";
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public LayerMask finishLayer;
    public float tileSize = 1f;

    private List<System.Action> finalActions;
    private bool isExecutingPath = false;
    private List<GameObject> availableDirectionTiles = new List<GameObject>();
    private List<ToolPlacement> toolPlacements = new List<ToolPlacement>();

    void Start()
    {
        if (cubeController == null) cubeController = GetComponent<DickControlledCube>();
        if (gridMover == null) gridMover = FindObjectOfType<GridObjectMover>();
    }

    public void StartBot()
    {
        if (isExecutingPath) return;
        StopAllCoroutines();
        StartCoroutine(StrategicSolver());
    }

    private bool IsFinishTrigger(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, 0.3f, finishLayer);
        return colliders.Length > 0;
    }

    private IEnumerator StrategicSolver()
    {
        Debug.Log("=== STRATEGIC BOT STARTED ===");
        
        // Анализ уровня
        LevelAnalysisResult analysis = AnalyzeLevel();
        Debug.Log($"Analysis: {analysis.AvailableDirectionTilesCount} direction tiles");

        // Выбор стратегии
        if (analysis.HasDirectionTiles && analysis.AvailableDirectionTilesCount > 0)
        {
            Debug.Log("Using Direction Tile strategy");
            yield return StartCoroutine(SolveWithDirectionTiles(analysis));
        }
        else
        {
            Debug.Log("Using Simple strategy");
            yield return StartCoroutine(SolveSimpleLevel());
        }
    }

    private LevelAnalysisResult AnalyzeLevel()
    {
        LevelAnalysisResult result = new LevelAnalysisResult();
        
        // Находим ВСЕ поворотные тайлы на сцене
        availableDirectionTiles = new List<GameObject>(GameObject.FindGameObjectsWithTag(directionTileTag));
        result.AvailableDirectionTilesCount = availableDirectionTiles.Count;
        result.HasDirectionTiles = result.AvailableDirectionTilesCount > 0;

        return result;
    }

    // ОСНОВНАЯ СТРАТЕГИЯ ДЛЯ ВЕКТОРОВ
    private IEnumerator SolveWithDirectionTiles(LevelAnalysisResult analysis)
    {
        toolPlacements.Clear();
        
        Vector3 finishPos = FindFinishPosition();
        Vector3 startPos = cubeController.InitialPosition;
        Vector3 startDir = cubeController.InitialDirection;

        Debug.Log($"Start: {startPos}, Finish: {finishPos}, StartDir: {startDir}");

        // Ищем все возможные пути
        List<GridPath> allPaths = new List<GridPath>();
        FindAllPaths(startPos, startDir, finishPos, new List<Vector3>(), 0, allPaths, analysis.AvailableDirectionTilesCount);

        Debug.Log($"Found {allPaths.Count} possible paths");

        // Выбираем лучший путь
        var feasiblePaths = allPaths.Where(p => p.requiredRotations <= analysis.AvailableDirectionTilesCount).ToList();
        
        if (feasiblePaths.Count == 0)
        {
            Debug.LogError("No feasible paths with available tiles!");
            yield break;
        }

        GridPath bestPath = feasiblePaths.OrderBy(p => p.requiredRotations).First();
        Debug.Log($"Best path: {bestPath.requiredRotations} rotations, {bestPath.cells.Count} steps");

        // АНАЛИЗИРУЕМ ГДЕ НУЖНЫ ПОВОРОТЫ И КАКИЕ
        AnalyzeRequiredRotations(bestPath);

        // РЕАЛИЗУЕМ ПУТЬ: сначала настраиваем тулы, потом двигаем куба
        yield return StartCoroutine(ImplementPathWithTools(bestPath));
    }

    // АНАЛИЗИРУЕМ ГДЕ НУЖНЫ ПОВОРОТЫ
    private void AnalyzeRequiredRotations(GridPath path)
    {
        Debug.Log("Analyzing required rotations...");
        
        Vector3 currentDir = cubeController.InitialDirection;
        
        for (int i = 0; i < path.cells.Count - 1; i++)
        {
            Vector3 currentCell = path.cells[i];
            Vector3 nextCell = path.cells[i + 1];
            
            // Определяем необходимое направление для перехода к следующей клетке
            Vector3 requiredDir = (nextCell - currentCell).normalized;
            
            // Если направление не совпадает с текущим - нужен поворот
            if (Vector3.Angle(currentDir, requiredDir) > 5f)
            {
                Debug.Log($"Rotation needed at cell {i} ({currentCell}): {currentDir} -> {requiredDir}");
                
                // Сохраняем информацию для поворота
                toolPlacements.Add(new ToolPlacement {
                    cellPosition = currentCell,
                    currentDirection = currentDir,
                    requiredDirection = requiredDir
                });
                
                currentDir = requiredDir; // Обновляем текущее направление
            }
        }
    }

    // РЕАЛИЗАЦИЯ ПУТИ С НАСТРОЙКОЙ ТУЛОВ
    private IEnumerator ImplementPathWithTools(GridPath path)
    {
        finalActions = new List<System.Action>();
        
        // 1. ФАЗА НАСТРОЙКИ ТУЛОВ - показываем решение
        Debug.Log("=== PHASE 1: SETUP TOOLS ===");
        
        if (toolPlacements.Count > 0)
        {
            Debug.Log($"Need to place {toolPlacements.Count} tools");
            
            foreach (var placement in toolPlacements)
            {
                // Находим свободный тайл для использования
                GameObject availableTile = FindAvailableDirectionTile();
                if (availableTile != null)
                {
                    // Добавляем действие по установке и повороту тайла
                    finalActions.Add(() => {
                        Debug.Log($"Placing tool at {placement.cellPosition}, rotating to {placement.requiredDirection}");
                        PlaceAndRotateTool(availableTile, placement.cellPosition, placement.requiredDirection);
                    });
                }
            }
        }
        else
        {
            Debug.Log("No tools needed for this path");
        }

        // 2. ФАЗА ДВИЖЕНИЯ - запускаем куба по готовому пути
        Debug.Log("=== PHASE 2: MOVEMENT ===");
        
        foreach (var cell in path.cells)
        {
            if (cell != cubeController.InitialPosition)
            {
                finalActions.Add(() => {
                    cubeController.ExecuteBotMove(cell, cubeController.InitialDirection);
                });
            }
        }

        // Включаем режим редактирования и выполняем ВСЕ действия
        gridMover.ForceEnableEditMode();
        yield return StartCoroutine(ExecutePath());
        gridMover.ForceDisableEditMode();
    }

    // ПОИСК СВОБОДНОГО ТАЙЛА ДЛЯ ИСПОЛЬЗОВАНИЯ
    private GameObject FindAvailableDirectionTile()
    {
        if (availableDirectionTiles.Count == 0) return null;
        
        // Берем первый доступный тайл
        GameObject tile = availableDirectionTiles[0];
        availableDirectionTiles.RemoveAt(0);
        return tile;
    }

    // УСТАНОВКА И ПОВОРОТ ТАЙЛА
    private void PlaceAndRotateTool(GameObject tool, Vector3 position, Vector3 direction)
    {
        // Перемещаем тайл в нужную позицию
        tool.transform.position = GetSnappedPosition(position);
        
        // Поворачиваем тайл в нужном направлении
        tool.transform.rotation = Quaternion.LookRotation(direction);
        
        Debug.Log($"Tool {tool.name} placed at {position} facing {direction}");
    }

    // РЕКУРСИВНЫЙ ПОИСК ПУТЕЙ (без изменений)
    private void FindAllPaths(Vector3 currentCell, Vector3 currentDir, Vector3 targetCell, 
                            List<Vector3> currentPath, int rotationsUsed, 
                            List<GridPath> foundPaths, int maxRotations)
    {
        currentPath.Add(currentCell);

        if (IsFinishTrigger(currentCell))
        {
            foundPaths.Add(new GridPath(new List<Vector3>(currentPath), rotationsUsed));
            return;
        }

        if (rotationsUsed > maxRotations || currentPath.Count > 20) return;

        // Движение вперед
        Vector3 nextCell = GetSnappedPosition(currentCell + currentDir * tileSize);
        if (IsCellValid(nextCell) && !currentPath.Contains(nextCell))
        {
            FindAllPaths(nextCell, currentDir, targetCell, new List<Vector3>(currentPath), 
                        rotationsUsed, foundPaths, maxRotations);
        }

        // Повороты
        if (rotationsUsed < maxRotations)
        {
            Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
            Vector3 rightCell = GetSnappedPosition(currentCell + rightDir * tileSize);
            
            if (IsCellValid(rightCell) && !currentPath.Contains(rightCell))
            {
                FindAllPaths(rightCell, rightDir, targetCell, new List<Vector3>(currentPath), 
                            rotationsUsed + 1, foundPaths, maxRotations);
            }

            Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;
            Vector3 leftCell = GetSnappedPosition(currentCell + leftDir * tileSize);
            
            if (IsCellValid(leftCell) && !currentPath.Contains(leftCell))
            {
                FindAllPaths(leftCell, leftDir, targetCell, new List<Vector3>(currentPath), 
                            rotationsUsed + 1, foundPaths, maxRotations);
            }
        }
    }

    private bool IsCellValid(Vector3 cellPosition)
    {
        bool hasGround = Physics.Raycast(cellPosition + Vector3.up * 0.5f, Vector3.down, 1.2f, groundLayer);
        bool hasObstacle = Physics.CheckBox(cellPosition, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, obstacleLayer);
        return hasGround && !hasObstacle;
    }

    private Vector3 GetSnappedPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / tileSize) * tileSize,
            position.y,
            Mathf.Round(position.z / tileSize) * tileSize
        );
    }

    private Vector3 FindFinishPosition()
    {
        GameObject finish = GameObject.FindGameObjectsWithTag("Finish").FirstOrDefault();
        return finish != null ? finish.transform.position : Vector3.zero;
    }

    private IEnumerator ExecutePath()
    {
        Debug.Log($"Executing {finalActions.Count} actions...");
        
        foreach (var action in finalActions)
        {
            if (IsFinishTrigger(transform.position))
            {
                Debug.Log("Finish reached during execution!");
                yield break;
            }
            
            action.Invoke();
            yield return new WaitForSeconds(setupWaitTime);
            
            if (IsFinishTrigger(transform.position))
            {
                Debug.Log("Finish reached after action!");
                yield break;
            }
        }
    }
    private IEnumerator SolveSimpleLevel()
    {
        Debug.Log("Solving simple level - direct movement");
        
        Vector3 finishPos = FindFinishPosition();
        Vector3 currentPos = cubeController.InitialPosition;
        Vector3 currentDir = cubeController.InitialDirection;

        Debug.Log($"Start: {currentPos}, Finish: {finishPos}, Dir: {currentDir}");

        // Просто двигаемся прямо к финишу
        List<Vector3> path = new List<Vector3>();
        path.Add(currentPos);

        int maxSteps = Mathf.CeilToInt(Vector3.Distance(currentPos, finishPos) / tileSize) + 5;
        
        for (int step = 0; step < maxSteps; step++)
        {
            if (IsFinishTrigger(currentPos))
            {
                Debug.Log("Reached finish during path planning!");
                break;
            }

            Vector3 nextCell = GetSnappedPosition(currentPos + currentDir * tileSize);
            
            if (!IsCellValid(nextCell))
            {
                Debug.LogError($"Path blocked at step {step}! Cell: {nextCell}");
                yield break;
            }

            path.Add(nextCell);
            currentPos = nextCell;
        }

        Debug.Log($"Planned path with {path.Count} steps");
        
        // Выполняем путь
        finalActions = new List<System.Action>();
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 targetCell = path[i];
            finalActions.Add(() => {
                cubeController.ExecuteBotMove(targetCell, cubeController.InitialDirection);
            });
        }

        yield return StartCoroutine(ExecutePath());
    }

    // ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
    public class LevelAnalysisResult
    {
        public bool HasDirectionTiles;
        public int AvailableDirectionTilesCount;
    }

    public class GridPath
    {
        public List<Vector3> cells;
        public int requiredRotations;

        public GridPath(List<Vector3> pathCells, int rotations)
        {
            cells = pathCells;
            requiredRotations = rotations;
        }
    }

    public class ToolPlacement
    {
        public Vector3 cellPosition;
        public Vector3 currentDirection;
        public Vector3 requiredDirection;
    }
}