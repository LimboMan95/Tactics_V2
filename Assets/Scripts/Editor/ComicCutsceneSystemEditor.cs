using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComicCutsceneSystem))]
public class ComicCutsceneSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var system = (ComicCutsceneSystem)target;
        if (system == null) return;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(system.previewSequence == null))
        {
            if (GUILayout.Button("Show Preview"))
            {
                system.EditorPreviewSelected();
                EditorUtility.SetDirty(system);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Prev Page"))
            {
                system.EditorPreviewPreviousPage();
                EditorUtility.SetDirty(system);
            }
            if (GUILayout.Button("Next Page"))
            {
                system.EditorPreviewNextPage();
                EditorUtility.SetDirty(system);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Frame"))
            {
                system.EditorPreviewRevealOne();
                EditorUtility.SetDirty(system);
            }
            if (GUILayout.Button("2 Frames"))
            {
                system.EditorPreviewRevealTwo();
                EditorUtility.SetDirty(system);
            }
            if (GUILayout.Button("3 Frames"))
            {
                system.EditorPreviewRevealThree();
                EditorUtility.SetDirty(system);
            }
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Clear Preview"))
        {
            system.EditorClearPreview();
            EditorUtility.SetDirty(system);
        }
    }
}

