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

    public static string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, SaveFileName); }
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
    }

    private static void Write(StorySaveData data)
    {
        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }
}
