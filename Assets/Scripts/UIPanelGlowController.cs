using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIPanelGlowController : MonoBehaviour
{
    [Header("Glow")]
    [Min(0f)] public float glowThickness = 20f;
    [Range(0f, 1f)] public float glowIntensity = 0.25f;
    public Color glowColor = Color.white;
    [Range(0f, 1f)] public float innerStrength = 0.6f;
    [Range(0f, 1f)] public float outerStrength = 0.4f;

    [Header("Editor")]
    public bool liveUpdateInEditor = false;

    [Header("Targets")]
    public bool autoCollect = true;
    public bool includeInactive = true;
    public List<Image> targetPanels = new List<Image>();

    private bool _applyQueued;
    private bool _isApplying;
    private bool _suppressValidate;

    private void OnEnable()
    {
        if (!Application.isPlaying && !liveUpdateInEditor) return;
        RequestApply();
    }

    private void OnValidate()
    {
        if (_isApplying || _suppressValidate) return;
        if (!Application.isPlaying && !liveUpdateInEditor) return;
        RequestApply();
    }

    [ContextMenu("Apply Glow")]
    public void Apply()
    {
        if (_isApplying) return;
        _isApplying = true;
        _suppressValidate = true;
        try
        {
            if (autoCollect)
            {
                ApplyToAutoCollectedPanels();
            }
            else
            {
                ApplyToListedPanels();
            }
        }
        finally
        {
            _suppressValidate = false;
            _isApplying = false;
        }
    }

    private void RequestApply()
    {
        if (_isApplying || _suppressValidate) return;
        if (Application.isPlaying)
        {
            Apply();
            return;
        }

#if UNITY_EDITOR
        if (_applyQueued) return;
        _applyQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyQueued;
#endif
    }

    private void ApplyQueued()
    {
        _applyQueued = false;
        if (this == null) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyQueued;
#endif
        Apply();
    }

    private void ApplyToListedPanels()
    {
        for (int i = 0; i < targetPanels.Count; i++)
        {
            var panel = targetPanels[i];
            if (!IsPanelTarget(panel)) continue;
            ApplyToPanel(panel);
        }
    }

    private void ApplyToAutoCollectedPanels()
    {
        var images = GetComponentsInChildren<Image>(includeInactive);
        for (int i = 0; i < images.Length; i++)
        {
            var panel = images[i];
            if (!IsPanelTarget(panel)) continue;
            ApplyToPanel(panel);
        }
    }

    private void ApplyToPanel(Image panel)
    {
        EnsureGlow(panel, out Image outer, out Image inner);
        ApplyGlowVisual(panel, outer, inner);
    }

    private static bool IsPanelTarget(Image image)
    {
        if (image == null) return false;
        if (!image.gameObject.name.StartsWith("Panel")) return false;
        if (IsGlowImage(image)) return false;
        return true;
    }

    private static bool IsGlowImage(Image image)
    {
        if (image == null) return true;
        var name = image.gameObject.name;
        if (name == "Glow Outer" || name == "Glow Inner") return true;
        return image.transform.parent != null && (image.transform.parent.name == "Glow Outer" || image.transform.parent.name == "Glow Inner");
    }

    private static void EnsureGlow(Image panel, out Image outer, out Image inner)
    {
        outer = FindOrCreateGlowImage(panel.transform, "Glow Outer");
        inner = FindOrCreateGlowImage(panel.transform, "Glow Inner");

        if (outer != null && outer.transform.GetSiblingIndex() != 0) outer.transform.SetSiblingIndex(0);
        if (inner != null && inner.transform.GetSiblingIndex() != 1) inner.transform.SetSiblingIndex(1);
    }

    private static Image FindOrCreateGlowImage(Transform panelTransform, string name)
    {
        var existing = panelTransform.Find(name);
        if (existing != null)
        {
            var img = existing.GetComponent<Image>();
            if (img != null) return img;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(panelTransform, false);
        return go.GetComponent<Image>();
    }

    private void ApplyGlowVisual(Image panel, Image outer, Image inner)
    {
        var sprite = panel.sprite;

        float thickness = glowThickness;
        float innerPad = thickness * 0.6f;
        float outerPad = thickness;

        SetupGlowImage(outer, sprite, panel.type, outerPad, glowIntensity * outerStrength);
        SetupGlowImage(inner, sprite, panel.type, innerPad, glowIntensity * innerStrength);
    }

    private void SetupGlowImage(Image glowImage, Sprite sprite, Image.Type type, float pad, float alpha)
    {
        if (glowImage == null) return;

        var rt = glowImage.rectTransform;
        if (rt.anchorMin != Vector2.zero) rt.anchorMin = Vector2.zero;
        if (rt.anchorMax != Vector2.one) rt.anchorMax = Vector2.one;

        var pivot = new Vector2(0.5f, 0.5f);
        if (rt.pivot != pivot) rt.pivot = pivot;

        if (rt.anchoredPosition != Vector2.zero) rt.anchoredPosition = Vector2.zero;
        if (rt.localRotation != Quaternion.identity) rt.localRotation = Quaternion.identity;
        if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;

        var offsetMin = new Vector2(-pad, -pad);
        var offsetMax = new Vector2(pad, pad);
        if (rt.offsetMin != offsetMin) rt.offsetMin = offsetMin;
        if (rt.offsetMax != offsetMax) rt.offsetMax = offsetMax;

        if (glowImage.sprite != sprite) glowImage.sprite = sprite;
        if (glowImage.type != type) glowImage.type = type;
        if (glowImage.preserveAspect) glowImage.preserveAspect = false;
        if (glowImage.material != null) glowImage.material = null;
        if (glowImage.raycastTarget) glowImage.raycastTarget = false;

        var c = glowColor;
        c.a = Mathf.Clamp01(alpha);
        if (glowImage.color != c) glowImage.color = c;
    }
}
