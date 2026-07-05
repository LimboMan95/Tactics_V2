public enum GameFlowMode
{
    Unset = 0,
    Story = 1,
    LevelSelect = 2
}

public static class GameFlowState
{
    public static GameFlowMode CurrentMode { get; private set; } = GameFlowMode.Unset;

    public static bool ShouldPlayComics
    {
        get
        {
            return CurrentMode == GameFlowMode.Story || CurrentMode == GameFlowMode.Unset;
        }
    }

    public static void SetStoryMode()
    {
        CurrentMode = GameFlowMode.Story;
    }

    public static void SetLevelSelectMode()
    {
        CurrentMode = GameFlowMode.LevelSelect;
    }

    public static void ResetToUnset()
    {
        CurrentMode = GameFlowMode.Unset;
    }
}

