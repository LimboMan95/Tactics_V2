using System;
using System.IO;
using UnityEngine;

public enum StoryResumeType
{
    None = 0,
    Level = 1,
    Comic = 2
}

[Serializable]
public class StorySaveData
{
    public int version = 1;
    public StoryResumeType resumeType = StoryResumeType.None;
    public int levelBuildIndex = -1;
    public int comicTriggerAfterLevelIndex = -1;
    public int nextSceneBuildIndexAfterComic = -1;
}

public static class StorySaveManager
{
    private const string SaveFileName = "story_save.txt";
    private static string _fallbackPersistentSavePath;

    public static string SavePath
    {
        get { return GetPrimarySavePath(); }
    }

    public static StorySaveData LoadOrCreate()
    {
        Debug.Log($"StorySave path: {SavePath}");

        if (!File.Exists(SavePath))
        {
            var fresh = CreateDefaultData();
            Write(fresh);
            return fresh;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                var fresh = CreateDefaultData();
                Write(fresh);
                return fresh;
            }

            var data = JsonUtility.FromJson<StorySaveData>(json);
            if (data == null)
            {
                data = CreateDefaultData();
                Write(data);
            }
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Story save load failed: {ex.Message}");
            var fresh = CreateDefaultData();
            Write(fresh);
            return fresh;
        }
    }

    public static void SaveLevelResume(int levelBuildIndex)
    {
        var data = LoadOrCreate();
        data.resumeType = StoryResumeType.Level;
        data.levelBuildIndex = levelBuildIndex;
        data.comicTriggerAfterLevelIndex = -1;
        data.nextSceneBuildIndexAfterComic = -1;
        Write(data);
    }

    public static void SaveComicResume(int comicTriggerAfterLevelIndex, int nextSceneBuildIndexAfterComic)
    {
        var data = LoadOrCreate();
        data.resumeType = StoryResumeType.Comic;
        data.levelBuildIndex = -1;
        data.comicTriggerAfterLevelIndex = comicTriggerAfterLevelIndex;
        data.nextSceneBuildIndexAfterComic = nextSceneBuildIndexAfterComic;
        Write(data);
    }

    public static StorySaveData CreateDefaultData()
    {
        return new StorySaveData();
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        if (!string.IsNullOrEmpty(_fallbackPersistentSavePath) && File.Exists(_fallbackPersistentSavePath))
        {
            File.Delete(_fallbackPersistentSavePath);
        }
    }

    private static void Write(StorySaveData data)
    {
        string path = SavePath;
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(data, true);
        try
        {
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            string fallbackPath = GetFallbackPersistentSavePath();
            string fallbackDirectory = Path.GetDirectoryName(fallbackPath);
            if (!string.IsNullOrEmpty(fallbackDirectory) && !Directory.Exists(fallbackDirectory))
            {
                Directory.CreateDirectory(fallbackDirectory);
            }

            File.WriteAllText(fallbackPath, json);
            Debug.LogWarning($"Story save write fallback to persistent path: {ex.Message}");
        }
    }

    private static string GetPrimarySavePath()
    {
        string dataPath = Application.dataPath;
        string rootDirectory = Directory.GetParent(dataPath)?.FullName;

        if (string.IsNullOrEmpty(rootDirectory))
        {
            return GetFallbackPersistentSavePath();
        }

        return Path.Combine(rootDirectory, SaveFileName);
    }

    private static string GetFallbackPersistentSavePath()
    {
        if (string.IsNullOrEmpty(_fallbackPersistentSavePath))
        {
            _fallbackPersistentSavePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        return _fallbackPersistentSavePath;
    }
}
