using UnityEngine;
using System;
using System.Collections.Generic;

public class BombDetonatorPair : MonoBehaviour, IResettable
{
    [Serializable]
    public class Link
    {
        public Bomb bomb;
        public Detonator detonator;
        public bool syncBombColor = true;
    }

    public List<Link> links = new List<Link>();

    public Bomb bomb;
    public Detonator detonator;
    
    void Start()
    {
        if (links.Count == 0 && detonator != null && bomb != null)
        {
            links.Add(new Link
            {
                bomb = bomb,
                detonator = detonator,
                syncBombColor = true
            });
        }

        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            if (link == null || link.detonator == null || link.bomb == null) continue;

            link.detonator.onPressed.AddListener(link.bomb.Activate);
            if (link.syncBombColor)
            {
                link.bomb.bombColor = link.detonator.detonatorColor;
                var rend = link.bomb.GetComponent<Renderer>();
                if (rend != null) rend.material.color = link.detonator.detonatorColor;
            }
        }
    }
    
    public void HighlightBomb(bool highlight)
    {
        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            if (link == null || link.bomb == null) continue;
            link.bomb.Highlight(highlight);
        }
    }
    
    // Ресет для пары (если нужно)
    public void ResetObject()
    {
        // Ничего не делаем, так как бомба и детонатор сбрасываются сами
        Debug.Log("BombDetonatorPair reset");
    }
}
