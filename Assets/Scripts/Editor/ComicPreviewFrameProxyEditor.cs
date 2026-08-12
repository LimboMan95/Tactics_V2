using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComicPreviewFrameProxy))]
public class ComicPreviewFrameProxyEditor : Editor
{
    private void OnSceneGUI()
    {
        var proxy = (ComicPreviewFrameProxy)target;
        if (proxy == null || !proxy.PreviewEditable) return;

        var rect = proxy.RectTransform;
        if (rect == null) return;

        if (!proxy.TryGetFrame(out var frame)) return;

        Vector2 savedScale = frame.GetImageScale();
        Vector2 currentScale = new Vector2(
            proxy.BaseCoverSize.x > 0f ? rect.sizeDelta.x / proxy.BaseCoverSize.x : savedScale.x,
            proxy.BaseCoverSize.y > 0f ? rect.sizeDelta.y / proxy.BaseCoverSize.y : savedScale.y);
        Vector2 currentOffset = rect.anchoredPosition;

        if (Approximately(currentOffset, frame.imageOffset) && Approximately(currentScale, savedScale))
            return;

        Undo.RecordObject(proxy.PreviewSequence, "Adjust Comic Frame Transform");
        if (proxy.SaveTransform(currentOffset, currentScale))
        {
            EditorUtility.SetDirty(proxy.PreviewSequence);
        }
    }

    public override void OnInspectorGUI()
    {
        var proxy = (ComicPreviewFrameProxy)target;
        if (proxy == null)
            return;

        EditorGUILayout.HelpBox(
            "Используй Rect Tool в Scene view: двигай Sprite внутри рамки и масштабируй его. Изменения сразу сохраняются в ComicSequence.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Sequence", proxy.PreviewSequence, typeof(ComicSequence), false);
            EditorGUILayout.IntField("Page", proxy.PreviewPageIndex);
            EditorGUILayout.IntField("Frame", proxy.PreviewFrameIndex + 1);
            EditorGUILayout.Vector2Field("Base Cover Size", proxy.BaseCoverSize);
        }
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.01f &&
               Mathf.Abs(a.y - b.y) < 0.01f;
    }
}
