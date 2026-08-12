using System.Collections.Generic;
using UnityEngine;

public enum Act1Archetype
{
    Overwhelmed = 0,
    Cynic = 1,
    Controller = 2
}

public enum PatrickAnswerStyle
{
    None = 0,
    Honest = 1,
    Blef = 2,
    Avoid = 3,
    Paid = 4
}

public enum BreakingAnswer
{
    None = 0,
    OpenUp = 1,
    ShutDown = 2,
    FlipTable = 3
}

public enum FinalVariant
{
    None = 0,
    WarmDivan = 1,
    ControlBoss = 2,
    PassionBed = 3,
    ColdPaid = 4
}

public class StoryRuntimeState : ScriptableObject
{
    private static StoryRuntimeState _instance;

    public static StoryRuntimeState Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = CreateInstance<StoryRuntimeState>();
                _instance.name = "StoryRuntimeState_Default";
                _instance.ApplyArchetypePreset(Act1Archetype.Overwhelmed);
            }
            return _instance;
        }
    }

    [Header("Act 1 Archetype (start preset)")]
    public Act1Archetype archetype = Act1Archetype.Overwhelmed;

    [Header("Scales")]
    [Range(-100, 100)] public int CharlotteTrust = 0;
    [Range(0, 100)] public int PatrickPressure = 0;
    [Range(0, 100)] public int NickStress = 20;
    [Range(-100, 100)] public int NickWarmth = 0;

    [Header("Resources")]
    public int Money = 450;

    [Header("Flags (Act 1)")]
    public bool S1_BlefUsed;
    public bool S2_PaidPatrick;
    public bool S2_ExtraWorkRequired;

    [Header("Enums (Act 1)")]
    public PatrickAnswerStyle S2_PatrickAnswerStyle = PatrickAnswerStyle.None;
    public BreakingAnswer S3_BreakingPointAnswer = BreakingAnswer.None;
    public bool S3_Charlotte_KnowsAboutPatrick;
    public FinalVariant S3_FinalVariant = FinalVariant.None;

    [Header("History")]
    public List<string> chosenOptionIds = new List<string>();

    public static void ReplaceInstance(StoryRuntimeState newState)
    {
        if (newState == null) return;
        _instance = newState;
    }

    public void ApplyArchetypePreset(Act1Archetype arch)
    {
        archetype = arch;
        switch (arch)
        {
            case Act1Archetype.Overwhelmed:
                CharlotteTrust = 0;
                PatrickPressure = 20;
                NickStress = 45;
                NickWarmth = 10;
                Money = 450;
                break;
            case Act1Archetype.Cynic:
                CharlotteTrust = -20;
                PatrickPressure = 40;
                NickStress = 25;
                NickWarmth = -20;
                Money = 450;
                break;
            case Act1Archetype.Controller:
                CharlotteTrust = 10;
                PatrickPressure = 50;
                NickStress = 20;
                NickWarmth = 0;
                Money = 450;
                break;
        }
        chosenOptionIds.Clear();
        S1_BlefUsed = false;
        S2_PaidPatrick = false;
        S2_ExtraWorkRequired = false;
        S2_PatrickAnswerStyle = PatrickAnswerStyle.None;
        S3_BreakingPointAnswer = BreakingAnswer.None;
        S3_Charlotte_KnowsAboutPatrick = false;
        S3_FinalVariant = FinalVariant.None;
    }

    public void ResetFromCurrentArchetype()
    {
        ApplyArchetypePreset(archetype);
    }

    public string Summarize()
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "[arch={0}] CT={1} PP={2} NS={3} NW={4} $={5} | paid={6} extraWork={7}",
            archetype,
            CharlotteTrust,
            PatrickPressure,
            NickStress,
            NickWarmth,
            Money,
            S2_PaidPatrick,
            S2_ExtraWorkRequired);
    }
}
