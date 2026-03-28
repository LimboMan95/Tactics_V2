using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelCodeBatchExporter
{
    [MenuItem("Tools/Level Codes/Export All Scenes (Assets/Scenes)")]
    public static void ExportAllScenesInFolder()
    {
        var srcConfig = Object.FindFirstObjectByType<LevelCodeManager>();
        if (srcConfig == null)
        {
            EditorUtility.DisplayDialog("LevelCodeManager not found",
                "Открой сцену с настроенным LevelCodeManager (например, 20_export) и повторите команду.", "OK");
            return;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        if (sceneGuids == null || sceneGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Нет сцен", "В папке Assets/Scenes не найдено ни одной сцены.", "OK");
            return;
        }

        var scenePaths = sceneGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => Path.GetFileNameWithoutExtension(p))
            .ToList();

        DoExportBatch(scenePaths, srcConfig);
    }

    [MenuItem("Tools/Level Codes/Export All Scenes CSV (Assets/Scenes)")]
    public static void ExportAllScenesInFolderCsv()
    {
        var srcConfig = Object.FindFirstObjectByType<LevelCodeManager>();
        if (srcConfig == null)
        {
            EditorUtility.DisplayDialog("LevelCodeManager not found",
                "Открой сцену с настроенным LevelCodeManager (например, 20_export) и повторите команду.", "OK");
            return;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        if (sceneGuids == null || sceneGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Нет сцен", "В папке Assets/Scenes не найдено ни одной сцены.", "OK");
            return;
        }

        var scenePaths = sceneGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => Path.GetFileNameWithoutExtension(p))
            .ToList();

        DoExportBatchCsv(scenePaths, srcConfig);
    }

    [MenuItem("Tools/Level Codes/Export Scenes From Build Settings")]
    public static void ExportBuildSettingsScenes()
    {
        var srcConfig = Object.FindFirstObjectByType<LevelCodeManager>();
        if (srcConfig == null)
        {
            EditorUtility.DisplayDialog("LevelCodeManager not found",
                "Открой сцену с настроенным LevelCodeManager (например, 20_export) и повторите команду.", "OK");
            return;
        }

        var scenePaths = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToList();

        if (scenePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Нет сцен", "В Build Settings нет включённых сцен.", "OK");
            return;
        }

        DoExportBatch(scenePaths, srcConfig);
    }

    [MenuItem("Tools/Level Codes/Export Scenes CSV From Build Settings")]
    public static void ExportBuildSettingsScenesCsv()
    {
        var srcConfig = Object.FindFirstObjectByType<LevelCodeManager>();
        if (srcConfig == null)
        {
            EditorUtility.DisplayDialog("LevelCodeManager not found",
                "Открой сцену с настроенным LevelCodeManager (например, 20_export) и повторите команду.", "OK");
            return;
        }

        var scenePaths = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToList();

        if (scenePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Нет сцен", "В Build Settings нет включённых сцен.", "OK");
            return;
        }

        DoExportBatchCsv(scenePaths, srcConfig);
    }

    private static void DoExportBatch(List<string> scenePaths, LevelCodeManager srcConfig)
    {
        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save Level Codes",
            "LevelCodes.txt",
            "txt",
            "Выберите файл для сохранения списка кодов уровней");
        if (string.IsNullOrEmpty(savePath))
        {
            savePath = "Assets/LevelCodes.txt";
        }

        // Сохраняем текущую сцену, чтобы вернуться после экспорта
        var currentScenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentScenePath))
        {
            currentScenePath = null;
        }

        var sb = new StringBuilder(1024);
        sb.AppendLine("# Level Codes");
        sb.AppendLine("# Формат: <SceneName> = <Code>");
        sb.AppendLine();

        for (int i = 0; i < scenePaths.Count; i++)
        {
            string path = scenePaths[i];
            string sceneName = Path.GetFileNameWithoutExtension(path);
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogWarning($"Не удалось открыть сцену: {path}");
                    continue;
                }

                // Ищем LevelRoot по имени, если нет — экспортируем всю сцену
                Transform levelRoot = null;
                var levelRootGO = GameObject.Find("LevelRoot");
                if (levelRootGO != null)
                    levelRoot = levelRootGO.transform;

                // Создаём временный экспортёр и копируем настройки
                var tempGO = new GameObject("__TempLevelCodeExporter__");
                var exporter = tempGO.AddComponent<LevelCodeManager>();
                CopyConfig(srcConfig, exporter);
                exporter.levelRoot = levelRoot;
                
                // Пытаемся подстроить tileSize под сцену (если у куба иной tileSize)
                var sceneCube = Object.FindFirstObjectByType<DickControlledCube>();
                if (sceneCube != null && sceneCube.tileSize > 1e-4f)
                {
                    exporter.tileSize = sceneCube.tileSize;
                }

                string code = exporter.ExportLevelCode();
                Object.DestroyImmediate(tempGO);

                sb.Append(sceneName);
                sb.Append(" = ");
                sb.AppendLine(code);

                Debug.Log($"[LevelCode] Экспортировано: {sceneName}");
            }
            catch (System.SystemException ex)
            {
                Debug.LogError($"Ошибка экспорта сцены {sceneName}: {ex.Message}");
            }
        }

        File.WriteAllText(savePath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
        {
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog("Готово", $"Коды уровней сохранены в:\n{savePath}", "OK");
    }

    private static void DoExportBatchCsv(List<string> scenePaths, LevelCodeManager srcConfig)
    {
        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save Level Codes CSV",
            "LevelCodes.csv",
            "csv",
            "Выберите CSV файл для сохранения списка кодов уровней");
        if (string.IsNullOrEmpty(savePath))
        {
            savePath = "Assets/LevelCodes.csv";
        }

        var currentScenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentScenePath))
        {
            currentScenePath = null;
        }

        var sb = new StringBuilder(1024);
        sb.AppendLine("ID сцены;Код;Уникальные тулы");

        for (int i = 0; i < scenePaths.Count; i++)
        {
            string path = scenePaths[i];
            string sceneName = Path.GetFileNameWithoutExtension(path);
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogWarning($"Не удалось открыть сцену: {path}");
                    continue;
                }

                Transform levelRoot = null;
                var levelRootGO = GameObject.Find("LevelRoot");
                if (levelRootGO != null)
                    levelRoot = levelRootGO.transform;

                var tempGO = new GameObject("__TempLevelCodeExporter__");
                var exporter = tempGO.AddComponent<LevelCodeManager>();
                CopyConfig(srcConfig, exporter);
                exporter.levelRoot = levelRoot;

                var sceneCube = Object.FindFirstObjectByType<DickControlledCube>();
                if (sceneCube != null && sceneCube.tileSize > 1e-4f)
                {
                    exporter.tileSize = sceneCube.tileSize;
                }

                string code = exporter.ExportLevelCode();
                string mechanics = string.Join(",", CollectMechanics(exporter, levelRoot));
                Object.DestroyImmediate(tempGO);

                sb.Append(EscapeCsv(sceneName));
                sb.Append(';');
                sb.Append(EscapeCsv(code));
                sb.Append(';');
                sb.AppendLine(EscapeCsv(mechanics));

                Debug.Log($"[LevelCode CSV] Экспортировано: {sceneName}");
            }
            catch (System.SystemException ex)
            {
                Debug.LogError($"Ошибка экспорта сцены {sceneName}: {ex.Message}");
            }
        }

        File.WriteAllText(savePath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
        {
            EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog("Готово", $"CSV сохранён в:\n{savePath}", "OK");
    }

    private static IEnumerable<string> CollectMechanics(LevelCodeManager exporter, Transform levelRoot)
    {
        var set = new HashSet<string>();

        IEnumerable<Transform> scope;
        if (levelRoot != null)
        {
            scope = levelRoot.GetComponentsInChildren<Transform>(true).Where(t => t != levelRoot);
        }
        else
        {
            scope = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true));
        }

        foreach (var t in scope)
        {
            if (t == null) continue;
            var go = t.gameObject;
            if (go == null) continue;

            if (go.GetComponent<Bomb>() != null) set.Add("бомба");
            if (go.GetComponent<Detonator>() != null) set.Add("детонатор");
            if (go.GetComponent<Crate>() != null) set.Add("ящик");

            if (go.GetComponent<FragileTile>() != null || (!string.IsNullOrEmpty(exporter.fragileTileTag) && go.CompareTag(exporter.fragileTileTag)))
                set.Add("хрупкий");

            if (!string.IsNullOrEmpty(exporter.directionTileTag) && go.CompareTag(exporter.directionTileTag))
                set.Add("вектор");
            if (!string.IsNullOrEmpty(exporter.jumpTileTag) && go.CompareTag(exporter.jumpTileTag))
                set.Add("джампер");
            if (!string.IsNullOrEmpty(exporter.speedTileTag) && go.CompareTag(exporter.speedTileTag))
                set.Add("ускорение");
        }

        return set.OrderBy(s => s);
    }

    private static string EscapeCsv(string value)
    {
        if (value == null) return string.Empty;
        bool needsQuotes = value.Contains(';') || value.Contains('\n') || value.Contains('\r') || value.Contains('"');
        if (!needsQuotes) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void CopyConfig(LevelCodeManager src, LevelCodeManager dst)
    {
        dst.tileSize = src.tileSize;
        dst.baseFloorPrefabId = src.baseFloorPrefabId;

        dst.directionTileTag = src.directionTileTag;
        dst.jumpTileTag = src.jumpTileTag;
        dst.speedTileTag = src.speedTileTag;
        dst.fragileTileTag = src.fragileTileTag;
        dst.finishTag = src.finishTag;

        dst.bombExplosionEffects = new List<LevelCodeManager.ExplosionEffectEntry>(src.bombExplosionEffects.Count);
        foreach (var e in src.bombExplosionEffects)
        {
            if (e == null) continue;
            var ne = new LevelCodeManager.ExplosionEffectEntry
            {
                id = e.id,
                key = e.key,
                prefab = e.prefab
            };
            dst.bombExplosionEffects.Add(ne);
        }

        dst.prefabs = new List<LevelCodeManager.PrefabEntry>(src.prefabs.Count);
        foreach (var e in src.prefabs)
        {
            if (e == null) continue;
            var ne = new LevelCodeManager.PrefabEntry
            {
                id = e.id,
                type = e.type,
                key = e.key,
                prefab = e.prefab,
                usesRotation = e.usesRotation,
                includeInBounds = e.includeInBounds
            };
            dst.prefabs.Add(ne);
        }
    }
}
