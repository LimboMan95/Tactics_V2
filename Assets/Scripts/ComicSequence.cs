using System;
using System.Collections.Generic;
using UnityEngine;

public enum ComicQteLockState
{
    Unlocked = 0,
    SoftLocked = 1,
    HardLocked = 2
}

public enum ComicConditionCheckType
{
    FlagSet = 0,
    FlagNotSet = 1,
    ScaleGreaterOrEqual = 2,
    ScaleLessOrEqual = 3,
    MoneyGreaterOrEqual = 4,
    MoneyLessOrEqual = 5,
    ArchetypeEqual = 6,
    OptionIdChosen = 7,
    OptionIdNotChosen = 8
}

public enum ComicConditionScale
{
    CharlotteTrust = 0,
    PatrickPressure = 1,
    NickStress = 2,
    NickWarmth = 3
}

[Serializable]
public struct ComicCondition
{
    public ComicConditionCheckType checkType;
    public string flagName;
    public ComicConditionScale scale;
    public int intValue;
    public Act1Archetype archetype;

    public bool Evaluate(StoryRuntimeState state)
    {
        if (state == null) return true;
        switch (checkType)
        {
            case ComicConditionCheckType.FlagSet:
                return GetFlagValue(state, flagName);
            case ComicConditionCheckType.FlagNotSet:
                return !GetFlagValue(state, flagName);
            case ComicConditionCheckType.ScaleGreaterOrEqual:
                return GetScaleValue(state, scale) >= intValue;
            case ComicConditionCheckType.ScaleLessOrEqual:
                return GetScaleValue(state, scale) <= intValue;
            case ComicConditionCheckType.MoneyGreaterOrEqual:
                return state.Money >= intValue;
            case ComicConditionCheckType.MoneyLessOrEqual:
                return state.Money <= intValue;
            case ComicConditionCheckType.ArchetypeEqual:
                return state.archetype == archetype;
            case ComicConditionCheckType.OptionIdChosen:
                return OptionIdInHistory(state, flagName);
            case ComicConditionCheckType.OptionIdNotChosen:
                return !OptionIdInHistory(state, flagName);
        }
        return true;
    }

    private static bool OptionIdInHistory(StoryRuntimeState state, string optionId)
    {
        if (state == null || string.IsNullOrEmpty(optionId)) return false;
        if (state.chosenOptionIds == null) return false;
        for (int i = 0; i < state.chosenOptionIds.Count; i++)
        {
            if (string.Equals(state.chosenOptionIds[i], optionId, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool GetFlagValue(StoryRuntimeState state, string name)
    {
        if (state == null || string.IsNullOrEmpty(name)) return false;
        switch (name)
        {
            case "S1_BlefUsed":
            case "S1BlefUsed":
                return state.S1_BlefUsed;
            case "S2_PaidPatrick":
            case "S2PaidPatrick":
                return state.S2_PaidPatrick;
            case "S2_ExtraWorkRequired":
            case "S2ExtraWorkRequired":
                return state.S2_ExtraWorkRequired;
            case "S3_Charlotte_KnowsAboutPatrick":
            case "S3CharlotteKnowsAboutPatrick":
                return state.S3_Charlotte_KnowsAboutPatrick;
            default:
                return false;
        }
    }

    private static int GetScaleValue(StoryRuntimeState state, ComicConditionScale s)
    {
        if (state == null) return 0;
        switch (s)
        {
            case ComicConditionScale.CharlotteTrust: return state.CharlotteTrust;
            case ComicConditionScale.PatrickPressure: return state.PatrickPressure;
            case ComicConditionScale.NickStress: return state.NickStress;
            case ComicConditionScale.NickWarmth: return state.NickWarmth;
        }
        return 0;
    }
}

[Serializable]
public struct ComicStatusPill
{
    public string pillId;
    public string label;
    public string valueText;
    public Color tintColor;
    public Sprite icon;
}

[Serializable]
public struct ComicQteEffects
{
    public int deltaCharlotteTrust;
    public int deltaPatrickPressure;
    public int deltaNickStress;
    public int deltaNickWarmth;
    public int deltaMoney;

    public bool setS1BlefUsed;
    public bool setS2PaidPatrick;
    public bool setS2ExtraWorkRequired;

    [TextArea(1, 4)] public string statusBannerAfterChoice;
}

public enum QteAfterChoiceAction
{
    ContinueRevealNextFrame = 0,
    JumpToRevealFrameIndex = 1,
    JumpToPageIndex = 2,
    JumpToSequenceIndex = 3,
    ContinueThenJumpToPageOrSequence = 4
}

[Serializable]
public struct ComicFrameInlineOverride
{
    [Range(0,2)] public int frameIndex;
    public Sprite sprite;
    public bool showTextPlate;
    [TextArea(2,6)] public string frameText;
    public Vector2 imageOffset;
    public Vector2 imageScale;
}

[Serializable]
public struct ComicQteOption
{
    public string optionId;
    [TextArea(1, 4)] public string label;
    [TextArea(1, 4)] public string subtitle;
    public ComicQteLockState lockState;
    [TextArea(1, 4)] public string lockReason;

    [Header("Dynamic Lock Overrides (evaluated against StoryRuntimeState)")]
    [Tooltip("If any condition passes and requireAll=false (or all pass and requireAll=true), lockState is overridden.")]
    public List<ComicCondition> softLockConditions;
    public bool softLockRequireAll;
    [TextArea(1, 4)] public string softLockReasonOverride;

    public List<ComicCondition> hardLockConditions;
    public bool hardLockRequireAll;
    [TextArea(1, 4)] public string hardLockReasonOverride;

    [Header("Effects (will apply to story state)")]
    public ComicQteEffects effects;

    [Header("Per-frame inline overrides on SAME page (applied right after choice)")]
    public List<ComicFrameInlineOverride> overrideFramesAfterChoice;

    [Header("Flow after choice")]
    public QteAfterChoiceAction afterChoiceAction;
    [Range(0,2)] public int jumpToRevealFrameIndex;
    public int nextPageIndexOverride;
    public int nextSequenceIndexOverride;

    public ComicQteLockState EvaluateLock(StoryRuntimeState state, out string finalReason)
    {
        finalReason = lockReason;
        ComicQteLockState final = lockState;

        if (state != null && hardLockConditions != null && hardLockConditions.Count > 0)
        {
            int match = 0;
            for (int i = 0; i < hardLockConditions.Count; i++)
            {
                bool ok = hardLockConditions[i].Evaluate(state);
                if (!ok && hardLockRequireAll) { match = -1; break; }
                if (ok) match++;
            }
            bool hit = hardLockRequireAll ? match != -1 : match > 0;
            if (match == -1) hit = false;
            if (hit)
            {
                final = ComicQteLockState.HardLocked;
                if (!string.IsNullOrWhiteSpace(hardLockReasonOverride)) finalReason = hardLockReasonOverride;
                return final;
            }
        }

        if (state != null && softLockConditions != null && softLockConditions.Count > 0)
        {
            int match = 0;
            for (int i = 0; i < softLockConditions.Count; i++)
            {
                bool ok = softLockConditions[i].Evaluate(state);
                if (!ok && softLockRequireAll) { match = -1; break; }
                if (ok) match++;
            }
            bool hit = softLockRequireAll ? match != -1 : match > 0;
            if (match == -1) hit = false;
            if (hit && final != ComicQteLockState.HardLocked)
            {
                final = ComicQteLockState.SoftLocked;
                if (!string.IsNullOrWhiteSpace(softLockReasonOverride)) finalReason = softLockReasonOverride;
            }
        }

        return final;
    }
}

[Serializable]
public struct ComicQtePrompt
{
    public bool enabled;
    [Tooltip("0 = after frame 0 reveal (first frame, immediately on enter page)\n1 = after frame 1 reveal (after click 1)\n2 = after frame 2 reveal (default, after click 2)")]
    [Range(0, 2)] public int showAfterFrameIndex;
    [TextArea(1, 6)] public string questionText;
    [Min(0f)] public float timerSeconds;
    public string timerExpiredDefaultOptionId;
    public List<ComicQteOption> options;
}

[Serializable]
public struct ComicStatusBarConfig
{
    public bool showStatusHeader;
    [TextArea(1, 4)] public string topBannerText;
    public List<ComicStatusPill> statusPills;
}

[Serializable]
public struct ComicFrameConditionalOverride
{
    public List<ComicCondition> conditions;
    public bool requireAllConditions;
    public Sprite overrideSprite;
    public string overrideText;
    public Vector2 overrideImageOffset;
    public Vector2 overrideImageScale;

    public bool Matches(StoryRuntimeState state)
    {
        if (conditions == null || conditions.Count == 0) return false;
        int match = 0;
        for (int i = 0; i < conditions.Count; i++)
        {
            bool ok = conditions[i].Evaluate(state);
            if (!ok && requireAllConditions) return false;
            if (ok) match++;
        }
        return requireAllConditions ? true : match > 0;
    }
}

[Serializable]
public struct ComicFrame
{
    public Sprite sprite;
    public bool showTextPlate;
    [TextArea(2, 6)] public string frameText;
    public Vector2 imageOffset;
    public Vector2 imageScale;

    public List<ComicFrameConditionalOverride> conditionalOverrides;

    public Vector2 GetImageScale()
    {
        return imageScale == Vector2.zero ? Vector2.one : imageScale;
    }

    public ComicFrame Resolved(StoryRuntimeState state)
    {
        if (state == null || conditionalOverrides == null || conditionalOverrides.Count == 0) return this;
        ComicFrame result = this;
        for (int i = 0; i < conditionalOverrides.Count; i++)
        {
            var ov = conditionalOverrides[i];
            if (!ov.Matches(state)) continue;
            if (ov.overrideSprite != null) result.sprite = ov.overrideSprite;
            if (!string.IsNullOrWhiteSpace(ov.overrideText)) result.frameText = ov.overrideText;
            if (ov.overrideImageOffset != Vector2.zero) result.imageOffset = ov.overrideImageOffset;
            if (ov.overrideImageScale != Vector2.zero && ov.overrideImageScale != Vector2.one) result.imageScale = ov.overrideImageScale;
            break;
        }
        return result;
    }
}

public enum ComicPageGameOverBehaviour
{
    None = 0,
    QuitToMainMenu = 1,
    ResetSaveAndQuitToMainMenu = 2
}

[Serializable]
public struct ComicPage
{
    public ComicFrame frame0;
    public ComicFrame frame1;
    public ComicFrame frame2;

    public ComicStatusBarConfig statusBar;
    public ComicQtePrompt qte;

    [Header("After this page flow")]
    [Tooltip("-1 = go to next page sequentially (default). >=0 = jump to this exact page index in same sequence.")]
    public int afterThisPageJumpToPageIndex;
    [Tooltip("-1 = default. If >=0 AND afterThisPageJumpToPageIndex also set, jump to this sequence index after page.")]
    public int afterThisPageJumpToSequenceIndex;

    [Header("Game Over (Ending)")]
    public bool isGameOverPage;
    public ComicPageGameOverBehaviour gameOverBehaviour;
    [TextArea(2, 6)] public string gameOverBannerText;

    public ComicFrame GetFrame(int index)
    {
        if (index == 0) return frame0;
        if (index == 1) return frame1;
        return frame2;
    }

    public bool HasQte => qte.enabled && qte.options != null && qte.options.Count > 0;

    public int QteShowAfterFrameIndex => HasQte ? Mathf.Clamp(qte.showAfterFrameIndex, 0, 2) : -1;

    public bool TryGetPageJump(out int pageIndex)
    {
        pageIndex = afterThisPageJumpToPageIndex;
        return pageIndex >= 0;
    }

    public bool TryGetSequenceJump(out int sequenceIndex)
    {
        sequenceIndex = afterThisPageJumpToSequenceIndex;
        return sequenceIndex >= 0;
    }
}

[CreateAssetMenu(menuName = "Tactics V2/Comics/Comic Sequence", fileName = "ComicSequence")]
public class ComicSequence : ScriptableObject
{
    public int triggerAfterLevelIndex = 0;

    [Header("Trigger Conditions (optional)")]
    [Tooltip("If empty -> always matches. Else match using requireAllConditions against StoryRuntimeState.")]
    public List<ComicCondition> triggerConditions = new List<ComicCondition>();
    public bool requireAllConditions = true;

    public List<ComicPage> pages = new List<ComicPage>();

    public bool MatchesConditions(StoryRuntimeState state)
    {
        if (triggerConditions == null || triggerConditions.Count == 0) return true;
        int matchCount = 0;
        for (int i = 0; i < triggerConditions.Count; i++)
        {
            bool ok = triggerConditions[i].Evaluate(state);
            if (!ok && requireAllConditions) return false;
            if (ok) matchCount++;
        }
        return requireAllConditions ? true : matchCount > 0;
    }
}
