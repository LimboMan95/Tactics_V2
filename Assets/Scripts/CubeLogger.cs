using UnityEngine;
using System;
using System.Text;

public class CubeLogger : MonoBehaviour
{
    [Header("Log Settings")]
    public bool enableLogging = true;
    public bool logToConsole = true;
    public bool logToFile = false;
    public string logFileName = "cube_log.txt";
    
    [Header("Event Settings")]
    public bool logMovement = true;
    public bool logRotation = true;
    public bool logCollisions = true;
    public bool logGroundChanges = true;
    public bool logTileEvents = true;
    
    private StringBuilder logBuilder = new StringBuilder();
    private string lastGroundState;
    private Vector3 lastPosition;
    private Vector3 lastDirection;
    private DickControlledCube cubeController;

    void Start()
    {
        cubeController = GetComponent<DickControlledCube>();
        if (cubeController == null)
        {
            Debug.LogError("CubeLogger requires DickControlledCube component!");
            return;
        }

        lastGroundState = cubeController.IsGrounded ? "Grounded" : "Falling";
        lastPosition = transform.position;
        lastDirection = cubeController.CurrentDirection;
        
        LogSystemStart();
    }

    void Update()
    {
        if (!enableLogging || cubeController == null) return;
        
        CheckPositionChange();
        CheckDirectionChange();
        CheckGroundStateChange();
    }

    public void LogTileActivation(GameObject tile, Vector3 newDirection)
    {
        if (!enableLogging || !logTileEvents) return;
        
        string message = $"[ТАЙЛ] Активирован {tile.name} в позиции {FormatPosition(tile.transform.position)}\n" +
                       $"Новое направление: {FormatVector(newDirection)}";
        LogMessage(message);
    }

    public void LogCollision(Vector3 obstaclePosition, Vector3 newDirection)
    {
        if (!enableLogging || !logCollisions) return;
        
        string message = $"[СТОЛКНОВЕНИЕ] Препятствие в {FormatPosition(obstaclePosition)}\n" +
                       $"Новое направление: {FormatVector(newDirection)}";
        LogMessage(message, LogType.Warning);
    }

    public void LogRotationStart(Vector3 startDirection, Vector3 targetDirection)
    {
        if (!enableLogging || !logRotation) return;
        
        string message = $"[ПОВОРОТ] Начало поворота\n" +
                       $"От: {FormatVector(startDirection)}\n" +
                       $"К: {FormatVector(targetDirection)}";
        LogMessage(message);
    }

    public void LogRotationComplete(Vector3 newDirection)
    {
        if (!enableLogging || !logRotation) return;
        
        string message = $"[ПОВОРОТ] Поворот завершен\n" +
                       $"Новое направление: {FormatVector(newDirection)}";
        LogMessage(message);
    }

    public void LogFalling()
    {
        if (!enableLogging || !logGroundChanges) return;
        
        string message = $"[ПАДЕНИЕ] Куб начал падать в позиции {FormatPosition(transform.position)}";
        LogMessage(message, LogType.Warning);
    }

    public void LogLanding()
    {
        if (!enableLogging || !logGroundChanges) return;
        
        string message = $"[ЗЕМЛЯ] Куб приземлился в позиции {FormatPosition(transform.position)}";
        LogMessage(message);
    }

    private void CheckPositionChange()
    {
        if (!logMovement) return;
        
        if (Vector3.Distance(lastPosition, transform.position) > 0.1f)
        {
            string message = $"[ПЕРЕМЕЩЕНИЕ] Позиция изменилась\n" +
                           $"От: {FormatPosition(lastPosition)}\n" +
                           $"К: {FormatPosition(transform.position)}";
            LogMessage(message);
            lastPosition = transform.position;
        }
    }

    private void CheckDirectionChange()
    {
        if (!logRotation) return;
        
        if (Vector3.Angle(lastDirection, cubeController.CurrentDirection) > 5f)
        {
            string message = $"[НАПРАВЛЕНИЕ] Изменение направления\n" +
                           $"От: {FormatVector(lastDirection)}\n" +
                           $"К: {FormatVector(cubeController.CurrentDirection)}";
            LogMessage(message);
            lastDirection = cubeController.CurrentDirection;
        }
    }

    private void CheckGroundStateChange()
    {
        if (!logGroundChanges) return;
        
        string currentState = cubeController.IsGrounded ? "Grounded" : "Falling";
        if (lastGroundState != currentState)
        {
            if (cubeController.IsGrounded)
                LogLanding();
            else
                LogFalling();
            
            lastGroundState = currentState;
        }
    }

    private void LogSystemStart()
    {
        string message = $"Система логирования инициализирована {DateTime.Now}\n" +
                       $"Объект: {name}\n" +
                       $"Позиция: {FormatPosition(transform.position)}\n" +
                       $"Направление: {FormatVector(cubeController.CurrentDirection)}\n" +
                       $"Скорость: {cubeController.CurrentSpeed}\n" +
                       $"Состояние: {(cubeController.IsGrounded ? "На земле" : "В воздухе")}";
        
        LogMessage(message, LogType.Log, true);
    }

    private string FormatVector(Vector3 vec)
    {
        return $"({vec.x:F2}, {vec.y:F2}, {vec.z:F2})";
    }

    private string FormatPosition(Vector3 pos)
    {
        return $"X:{pos.x:F2}, Y:{pos.y:F2}, Z:{pos.z:F2}";
    }

    private void LogMessage(string message, LogType logType = LogType.Log, bool forceConsole = false)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] {message}";
        
        logBuilder.AppendLine(logEntry);

        if (logToConsole || forceConsole)
        {
            switch (logType)
            {
                case LogType.Warning:
                    Debug.LogWarning(logEntry);
                    break;
                case LogType.Error:
                    Debug.LogError(logEntry);
                    break;
                default:
                    Debug.Log(logEntry);
                    break;
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (logToFile && logBuilder.Length > 0)
        {
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
            System.IO.File.WriteAllText(filePath, logBuilder.ToString());
            Debug.Log($"Лог сохранен: {filePath}");
        }
    }
}