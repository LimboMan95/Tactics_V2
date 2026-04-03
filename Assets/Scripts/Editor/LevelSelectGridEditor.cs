using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelSelectGrid))]
public class LevelSelectGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var grid = (LevelSelectGrid)target;
        if (grid == null) return;

        GUILayout.Space(10);

        if (GUILayout.Button("Rebuild Now"))
        {
            grid.Rebuild();
            EditorUtility.SetDirty(grid);
        }

        if (GUILayout.Button("Clear Generated"))
        {
            grid.ClearGenerated();
            EditorUtility.SetDirty(grid);
        }
    }
}

