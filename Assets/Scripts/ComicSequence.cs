using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ComicFrame
{
    public Sprite sprite;
    public bool showTextPlate;
    [TextArea(2, 6)] public string frameText;
}

[Serializable]
public struct ComicPage
{
    public ComicFrame frame0;
    public ComicFrame frame1;
    public ComicFrame frame2;

    public ComicFrame GetFrame(int index)
    {
        if (index == 0) return frame0;
        if (index == 1) return frame1;
        return frame2;
    }
}

[CreateAssetMenu(menuName = "Tactics V2/Comics/Comic Sequence", fileName = "ComicSequence")]
public class ComicSequence : ScriptableObject
{
    public int triggerAfterLevelIndex = 0;
    public List<ComicPage> pages = new List<ComicPage>();
}

