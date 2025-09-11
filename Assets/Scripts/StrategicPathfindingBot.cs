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
    public string directionTileTag = "DirectionTile";
    public string jumpTileTag = "JumpTile";
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public LayerMask finishLayer; // <-- Убедись что этот слой настроен
    public float tileSize = 1f;

    private List<System.Action> finalActions;
    private bool isExecutingPath = false;
    private List<GameObject> availableDirectionTiles = new List<GameObject>();

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

    // ДОБАВЛЯЕМ МЕТОД ДЛЯ ПРОВЕРКИ ФИНИША
    private bool IsFinishTrigger(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos, 0.3f, finishLayer);
        bool isFinish = colliders.Length > 0;
        if (isFinish) Debug.Log($"Finish detected at position: {pos}");
        return isFinish;
    }

    private IEnumerator StrategicSolver()
    {
        Debug.Log("=== STRATEGIC BOT STARTED ===");
        
        // Анализ уровня
        LevelAnalysisResult analysis = AnalyzeLevel();
        Debug.Log($"Analysis: {analysis.AvailableDirectionTilesCount} direction tiles, {analysis.HasJumpTiles} jump tiles");

        // Выбор стратегии
        if (analysis.HasDirectionTiles && analysis.AvailableDirectionTilesCount > 0)
        {
            Debug.Log("Using Direction Tile strategy");
            yield return StartCoroutine(SolveWithDirectionTiles(analysis));
        }
        else if (analysis.HasJumpTiles)
        {
            Debug.Log("Using Jump Tile strategy");
            // yield return StartCoroutine(SolveWithJumpTiles(analysis));
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
        
        // Находим ВСЕ поворотные тайлы на сцене и сохраняем ссылки
        availableDirectionTiles = new List<GameObject>(GameObject.FindGameObjectsWithTag(directionTileTag));
        result.AvailableDirectionTilesCount = availableDirectionTiles.Count;
        result.HasDirectionTiles = result.AvailableDirectionTilesCount > 0;

        result.HasJumpTiles = GameObject.FindGameObjectsWithTag(jumpTileTag).Length > 0;

        return result;
    }

    // ОСНОВНАЯ СТРАТЕГИЯ ДЛЯ ВЕКТОРОВ
    private IEnumerator SolveWithDirectionTiles(LevelAnalysisResult analysis)
    {
        // 1. Находим финиш
        Vector3 finishPos = FindFinishPosition();
        Vector3 startPos = cubeController.InitialPosition;
        Vector3 startDir = cubeController.InitialDirection;

        Debug.Log($"Start: {startPos}, Finish: {finishPos}, StartDir: {startDir}");

        // 2. Ищем все возможные пути
        List<GridPath> allPaths = new List<GridPath>();
        FindAllPaths(startPos, startDir, finishPos, new List<Vector3>(), 0, allPaths, analysis.AvailableDirectionTilesCount);

        Debug.Log($"Found {allPaths.Count} possible paths");

        // 3. Фильтруем и выбираем лучший
        var feasiblePaths = allPaths.Where(p => p.requiredRotations <= analysis.AvailableDirectionTilesCount).ToList();
        
        if (feasiblePaths.Count == 0)
        {
            Debug.LogError("No feasible paths with available tiles!");
            yield break;
        }

        GridPath bestPath = feasiblePaths.OrderBy(p => p.requiredRotations).First();
        Debug.Log($"Best path: {bestPath.requiredRotations} rotations, {bestPath.cells.Count} steps");

        // 4. Визуализируем путь для дебага
        foreach (var cell in bestPath.cells)
        {
            Debug.DrawLine(cell, cell + Vector3.up * 2, Color.green, 5f);
        }

        // 5. Реализуем путь
        yield return StartCoroutine(ImplementDirectionPath(bestPath));
    }

    // РЕКУРСИВНЫЙ ПОИСК ВСЕХ ПУТЕЙ
    private void FindAllPaths(Vector3 currentCell, Vector3 currentDir, Vector3 targetCell, 
                            List<Vector3> currentPath, int rotationsUsed, 
                            List<GridPath> foundPaths, int maxRotations)
    {
        // Добавляем текущую клетку в путь
        currentPath.Add(currentCell);

        // ПРОВЕРЯЕМ ДОСТИГЛИ ЛИ МЫ ФИНИША (ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД)
        if (IsFinishTrigger(currentCell))
        {
            Debug.Log($"✓ Path found! Rotations: {rotationsUsed}, Steps: {currentPath.Count}");
            foundPaths.Add(new GridPath(new List<Vector3>(currentPath), rotationsUsed));
            return;
        }

        // Проверяем ограничения
        if (rotationsUsed > maxRotations) 
        {
            Debug.Log($"Path abandoned: too many rotations ({rotationsUsed})");
            return;
        }
        
        if (currentPath.Count > 20) 
        {
            Debug.Log($"Path abandoned: too long ({currentPath.Count} steps)");
            return;
        }

        // Пробуем двигаться вперед
        Vector3 nextCell = GetSnappedPosition(currentCell + currentDir * tileSize);
        if (IsCellValid(nextCell) && !currentPath.Contains(nextCell))
        {
            FindAllPaths(nextCell, currentDir, targetCell, new List<Vector3>(currentPath), 
                        rotationsUsed, foundPaths, maxRotations);
        }

        // Пробуем повороты (если есть доступные тайлы)
        if (rotationsUsed < maxRotations)
        {
            // Поворот направо (90°)
            Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
            Vector3 rightCell = GetSnappedPosition(currentCell + rightDir * tileSize);
            
            if (IsCellValid(rightCell) && !currentPath.Contains(rightCell))
            {
                FindAllPaths(rightCell, rightDir, targetCell, new List<Vector3>(currentPath), 
                            rotationsUsed + 1, foundPaths, maxRotations);
            }

            // Поворот налево (-90°)
            Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;
            Vector3 leftCell = GetSnappedPosition(currentCell + leftDir * tileSize);
            
            if (IsCellValid(leftCell) && !currentPath.Contains(leftCell))
            {
                FindAllPaths(leftCell, leftDir, targetCell, new List<Vector3>(currentPath), 
                            rotationsUsed + 1, foundPaths, maxRotations);
            }
        }
    }

    // ПРОВЕРКА ВАЛИДНОСТИ КЛЕТКИ
    private bool IsCellValid(Vector3 cellPosition)
    {
        // Проверяем землю
        bool hasGround = Physics.Raycast(cellPosition + Vector3.up * 0.5f, Vector3.down, 1.2f, groundLayer);
        if (!hasGround) return false;

        // Проверяем препятствия
        bool hasObstacle = Physics.CheckBox(cellPosition, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, obstacleLayer);
        
        return !hasObstacle;
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
        GameObject finish = GameObject.FindGameObjectWithTag("Finish");
        return finish != null ? finish.transform.position : Vector3.zero;
    }

    // ПРОСТОЙ УРОВЕНЬ - ПРЯМО К ФИНИШУ
    private IEnumerator SolveSimpleLevel()
    {
        Debug.Log("Solving simple level - checking direct path");
        
        Vector3 finishPos = FindFinishPosition();
        Vector3 currentPos = cubeController.InitialPosition;
        Vector3 currentDir = cubeController.InitialDirection;

        // Пробуем пройти прямо к финишу
        List<Vector3> path = new List<Vector3>();
        path.Add(currentPos);

        while (!IsFinishTrigger(currentPos) && path.Count < 20) // Используем проверку финиша
        {
            Vector3 nextCell = GetSnappedPosition(currentPos + currentDir * tileSize);
            
            if (!IsCellValid(nextCell))
            {
                Debug.LogError("Simple path blocked! Cannot reach finish directly.");
                yield break;
            }

            path.Add(nextCell);
            currentPos = nextCell;
        }

        // Выполняем путь
        finalActions = new List<System.Action>();
        foreach (var cell in path)
        {
            if (cell != cubeController.InitialPosition)
            {
                finalActions.Add(() => {
                    cubeController.ExecuteBotMove(cell, cubeController.InitialDirection);
                });
            }
        }

        yield return StartCoroutine(ExecutePath());
    }

    // РЕАЛИЗАЦИЯ ПУТИ С ПОМОЩЬЮ ТАЙЛОВ
    private IEnumerator ImplementDirectionPath(GridPath path)
    {
        finalActions = new List<System.Action>();

        foreach (var cell in path.cells)
        {
            if (cell != cubeController.InitialPosition)
            {
                finalActions.Add(() => {
                    cubeController.ExecuteBotMove(cell, cubeController.InitialDirection);
                });
            }
        }

        yield return StartCoroutine(ExecutePath());
    }

    private IEnumerator ExecutePath()
    {
        foreach (var action in finalActions)
        {
            // Проверяем не достигли ли мы уже финиша
            if (IsFinishTrigger(transform.position))
            {
                Debug.Log("Finish reached during execution!");
                yield break;
            }
            
            action.Invoke();
            yield return new WaitForSeconds(setupWaitTime);

            // Проверяем снова после выполнения действия
            if (IsFinishTrigger(transform.position))
            {
                Debug.Log("Finish reached after action!");
                yield break;
            }
        }
        
        // Финальная проверка
        if (IsFinishTrigger(transform.position))
        {
            Debug.Log("Finish reached after all actions!");
        }
        else
        {
            Debug.LogWarning("Path execution completed but finish not reached!");
        }
    }

    // ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ
    public class LevelAnalysisResult
    {
        public bool HasDirectionTiles;
        public bool HasJumpTiles;
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
}