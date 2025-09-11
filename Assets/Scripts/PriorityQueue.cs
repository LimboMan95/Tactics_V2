using System.Collections.Generic;

public class PriorityQueue<T>
{
    private List<(T item, int priority)> elements = new List<(T, int)>();
    
    public int Count => elements.Count;
    
    public void Enqueue(T item, int priority)
    {
        elements.Add((item, priority));
        // Сортируем по приоритету (меньше = выше)
        elements.Sort((a, b) => a.priority.CompareTo(b.priority));
    }
    
    public T Dequeue()
    {
        if (elements.Count == 0) 
            return default(T);
            
        var item = elements[0].item;
        elements.RemoveAt(0);
        return item;
    }
    
    public void Clear()
    {
        elements.Clear();
    }
    
    public bool Contains(T item)
    {
        return elements.Exists(x => x.item.Equals(item));
    }
}