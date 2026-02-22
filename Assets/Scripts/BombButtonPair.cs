using UnityEngine;

public class BombDetonatorPair : MonoBehaviour
{
    public Bomb bomb;
    public Detonator detonator;  // ← Было Button, стало Detonator
    
    void Start()
    {
        if (detonator != null && bomb != null)
        {
            detonator.onPressed.AddListener(() => bomb.Activate());
            bomb.bombColor = detonator.detonatorColor;  // ← Было buttonColor, стало detonatorColor
        }
    }
    
    public void HighlightBomb(bool highlight)
    {
        if (bomb != null) bomb.Highlight(highlight);
    }
}