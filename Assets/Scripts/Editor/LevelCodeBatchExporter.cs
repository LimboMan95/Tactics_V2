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
