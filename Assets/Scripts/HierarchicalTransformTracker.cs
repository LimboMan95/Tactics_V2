using UnityEngine;
using System.Collections.Generic;

public class HierarchicalTransformTracker : MonoBehaviour
{
    [System.Serializable]
    public class TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
    }

    public Transform parentObject; // Родительский объект
    public bool trackPosition = true;
    public bool trackRotation = true;
    public bool trackScale = false;
    public bool trackEveryFrame = true;

    private Dictionary<Transform, TransformSnapshot> _savedTransforms = 
        new Dictionary<Transform, TransformSnapshot>();

    void Start()
    {
        if (parentObject == null)
        {
            Debug.LogError("Parent object not assigned!");
            return;
        }

        InitializeChildrenTracking();
    }

    void Update()
    {
        if (trackEveryFrame)
        {
            UpdateAllTransforms();
        }
    }

    void InitializeChildrenTracking()
    {
        _savedTransforms.Clear();
        
        // Добавляем самого родителя
        SaveTransform(parentObject);

        // Добавляем всех детей
        foreach (Transform child in parentObject)
        {
            SaveTransform(child);
            TrackNestedChildren(child);
        }
    }

    void TrackNestedChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            SaveTransform(child);
            TrackNestedChildren(child); // Рекурсия для вложенных детей
        }
    }

    void SaveTransform(Transform target)
    {
        if (_savedTransforms.ContainsKey(target)) return;

        _savedTransforms[target] = new TransformSnapshot()
        {
            position = target.position,
            rotation = target.rotation,
            localScale = target.localScale
        };
    }

    void UpdateAllTransforms()
    {
        foreach (var entry in _savedTransforms)
        {
            UpdateTransform(entry.Key, entry.Value);
        }
    }

    void UpdateTransform(Transform target, TransformSnapshot snapshot)
    {
        if (trackPosition) snapshot.position = target.position;
        if (trackRotation) snapshot.rotation = target.rotation;
        if (trackScale) snapshot.localScale = target.localScale;
    }

    // API для получения данных
    public TransformSnapshot GetTransformData(Transform target)
    {
        if (_savedTransforms.TryGetValue(target, out var snapshot))
        {
            return snapshot;
        }
        return null;
    }

    public Dictionary<Transform, TransformSnapshot> GetAllTransforms()
    {
        return new Dictionary<Transform, TransformSnapshot>(_savedTransforms);
    }

    [ContextMenu("Print All Transforms")]
    void DebugPrintAllTransforms()
    {
        foreach (var entry in _savedTransforms)
        {
            Debug.Log($"{entry.Key.name}: " + 
                      $"Pos: {entry.Value.position} | " +
                      $"Rot: {entry.Value.rotation.eulerAngles}");
        }
    }
}