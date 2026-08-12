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
            if (GUILayout.Button("Show + Focus Preview"))
            {
                system.EditorFocusPreview();
                EditorUtility.SetDirty(system);
            }

            if (GUILayout.Button("Show Preview"))
            {
                system.EditorPreviewSelected();
                EditorUtility.SetDirty(system);
            }

            if (GUILayout.Button("Focus Preview"))
            {
                system.EditorFocusPreview();
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

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Frame Editing", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Frame 1"))
            {
                system.EditorSelectPreviewFrame(0);
            }
            if (GUILayout.Button("Select Frame 2"))
            {
                system.EditorSelectPreviewFrame(1);
            }
            if (GUILayout.Button("Select Frame 3"))
            {
                system.EditorSelectPreviewFrame(2);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Frame 1"))
            {
                system.EditorResetPreviewFrameTransform(0);
                EditorUtility.SetDirty(system.previewSequence);
            }
            if (GUILayout.Button("Reset Frame 2"))
            {
                system.EditorResetPreviewFrameTransform(1);
                EditorUtility.SetDirty(system.previewSequence);
            }
            if (GUILayout.Button("Reset Frame 3"))
            {
                system.EditorResetPreviewFrameTransform(2);
                EditorUtility.SetDirty(system.previewSequence);
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Show Preview -> Select Frame N -> двигай и масштабируюй Sprite в Scene view Rect Tool. Кадр остается обрезан рамкой, изменения пишутся в previewSequence.",
                MessageType.Info);
        }

        if (GUILayout.Button("Clear Preview"))
        {
            system.EditorClearPreview();
            EditorUtility.SetDirty(system);
        }
    }
}
