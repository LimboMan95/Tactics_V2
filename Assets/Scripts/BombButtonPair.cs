using UnityEngine;

public class BombDetonatorPair : MonoBehaviour, IResettable
{
    public Bomb bomb;
    public Detonator detonator;
    
    void Start()
    {
        if (detonator != null && bomb != null)
        {
            detonator.onPressed.AddListener(() => bomb.Activate());
            bomb.bombColor = detonator.detonatorColor;
        }
    }
    
    public void HighlightBomb(bool highlight)
    {
        if (bomb != null) bomb.Highlight(highlight);
    }
    
    // Ресет для пары (если нужно)
    public void ResetObject()
    {
        // Ничего не делаем, так как бомба и детонатор сбрасываются сами
        Debug.Log("BombDetonatorPair reset");
    }
}