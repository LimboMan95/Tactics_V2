using System;
using System.Collections.Generic;
using UnityEngine;

public class ToolPlacementVisual : MonoBehaviour
{
    public enum VisualState : byte
    {
        Normal = 0,
        Valid = 1,
        Invalid = 2,
        Rotating = 3,
        Activated = 4
    }

    [Serializable]
    public class RendererSwap
    {
        public Renderer renderer;
        public Material[] validMaterials;
        public Material[] invalidMaterials;
        public Material[] rotatingMaterials;
    }

    [Serializable]
    public class SlotOverride
    {
        public Renderer renderer;
        public Material sourceMaterial;
        public int materialIndex;
        public Material validMaterial;
        public Material invalidMaterial;
        public Material activatedMaterial;
        public Material rotatingMaterial;
    }

    public List<RendererSwap> rendererSwaps = new List<RendererSwap>();
    public List<SlotOverride> overrides = new List<SlotOverride>();

    private readonly Dictionary<Renderer, Material[]> _original = new Dictionary<Renderer, Material[]>();
    private bool _captured;

    private void Awake()
    {
        CaptureOriginal();
    }

    public void CaptureOriginal()
    {
        if (_captured) return;
        _original.Clear();

        for (int i = 0; i < rendererSwaps.Count; i++)
        {
            var s = rendererSwaps[i];
            if (s == null || s.renderer == null) continue;
            if (_original.ContainsKey(s.renderer)) continue;
            var mats = s.renderer.sharedMaterials;
            var copy = new Material[mats.Length];
            Array.Copy(mats, copy, mats.Length);
            _original[s.renderer] = copy;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            var o = overrides[i];
            if (o == null || o.renderer == null) continue;
            if (_original.ContainsKey(o.renderer)) continue;
            var mats = o.renderer.sharedMaterials;
            var copy = new Material[mats.Length];
            Array.Copy(mats, copy, mats.Length);
            _original[o.renderer] = copy;
        }

        _captured = true;
    }

    public void Apply(VisualState state)
    {
        if (!_captured) CaptureOriginal();

        for (int i = 0; i < rendererSwaps.Count; i++)
        {
            var s = rendererSwaps[i];
            if (s == null || s.renderer == null) continue;

            Material[] target = null;
            if (state == VisualState.Valid) target = s.validMaterials;
            else if (state == VisualState.Invalid) target = s.invalidMaterials;
            else if (state == VisualState.Rotating) target = s.rotatingMaterials != null && s.rotatingMaterials.Length > 0 ? s.rotatingMaterials : s.validMaterials;

            if (target == null || target.Length == 0) continue;
            s.renderer.sharedMaterials = target;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            var o = overrides[i];
            if (o == null || o.renderer == null) continue;
            if (!_original.TryGetValue(o.renderer, out var originalMats) || originalMats == null) continue;

            Material target = null;
            if (state == VisualState.Valid) target = o.validMaterial;
            else if (state == VisualState.Invalid) target = o.invalidMaterial;
            else if (state == VisualState.Rotating) target = o.rotatingMaterial != null ? o.rotatingMaterial : o.validMaterial;
            else if (state == VisualState.Activated) target = o.activatedMaterial;

            if (target == null) continue;

            var current = o.renderer.sharedMaterials;
            if (current.Length != originalMats.Length)
            {
                current = new Material[originalMats.Length];
                Array.Copy(originalMats, current, originalMats.Length);
            }

            if (o.sourceMaterial != null)
            {
                bool changed = false;
                for (int slot = 0; slot < originalMats.Length; slot++)
                {
                    if (originalMats[slot] == o.sourceMaterial)
                    {
                        current[slot] = target;
                        changed = true;
                    }
                }
                if (!changed) continue;
            }
            else
            {
                if (o.materialIndex < 0 || o.materialIndex >= originalMats.Length) continue;
                current[o.materialIndex] = target;
            }

            o.renderer.sharedMaterials = current;
        }
    }

    public void Restore()
    {
        if (!_captured) return;
        foreach (var kv in _original)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Key.sharedMaterials = kv.Value;
        }
    }
}
