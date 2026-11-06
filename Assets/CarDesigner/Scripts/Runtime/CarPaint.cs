using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarDesigner
{
    /// <summary>
    /// Body finishes. With a colour-palette texture the body's key colour is swapped texel by texel
    /// (keeping each texel's shading); without one the paint material's base colour is tinted directly.
    /// </summary>
    public class CarPaint : MonoBehaviour
    {
        [Serializable]
        public struct Finish
        {
            public string name;
            public Color color;
            [Range(0f, 1f)] public float smoothness;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [SerializeField] CarRig rig;
        [SerializeField] Material paintMaterial;
        [SerializeField] Texture2D palette;
        [SerializeField] Color paletteKey = Color.white;
        [SerializeField, Range(0.01f, 0.5f)] float keyTolerance = 0.16f;
        [SerializeField] int initialFinish;
        [SerializeField] Finish[] finishes =
        {
            new Finish { name = "Oxide Red", color = new Color32(0x8E, 0x2A, 0x1F, 0xFF), smoothness = 0.82f },
            new Finish { name = "Signal Orange", color = new Color32(0xD9, 0x62, 0x2B, 0xFF), smoothness = 0.78f },
            new Finish { name = "Bone", color = new Color32(0xD8, 0xD0, 0xC0, 0xFF), smoothness = 0.7f },
            new Finish { name = "Graphite", color = new Color32(0x2D, 0x2E, 0x30, 0xFF), smoothness = 0.86f },
            new Finish { name = "Olive Drab", color = new Color32(0x5B, 0x60, 0x40, 0xFF), smoothness = 0.35f },
            new Finish { name = "Racing Green", color = new Color32(0x1F, 0x3D, 0x2C, 0xFF), smoothness = 0.84f },
            new Finish { name = "Ochre", color = new Color32(0xC0, 0x8A, 0x2E, 0xFF), smoothness = 0.74f },
        };

        Material runtimeMaterial;
        Texture2D runtimeTexture;
        Color32[] sourceTexels;
        Color32[] workingTexels;

        public IReadOnlyList<Finish> Finishes => finishes;
        public int Current { get; private set; } = -1;
        public event Action<int> FinishChanged;

        void Awake()
        {
            if (paintMaterial == null) return;

            runtimeMaterial = new Material(paintMaterial) { name = paintMaterial.name + " (Runtime)" };
            foreach (CarPart part in rig.Parts) SwapMaterial(part.Renderer);

            if (palette != null && palette.isReadable)
            {
                sourceTexels = palette.GetPixels32();
                workingTexels = new Color32[sourceTexels.Length];
                runtimeTexture = new Texture2D(palette.width, palette.height, TextureFormat.RGBA32, false)
                {
                    name = palette.name + " (Runtime)",
                    filterMode = palette.filterMode,
                    wrapMode = palette.wrapMode
                };
                runtimeMaterial.SetTexture(BaseMapId, runtimeTexture);
            }
        }

        void Start() => Apply(initialFinish);

        void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }

        public void Apply(int index)
        {
            if (runtimeMaterial == null || index < 0 || index >= finishes.Length) return;

            Finish finish = finishes[index];
            runtimeMaterial.SetFloat(SmoothnessId, finish.smoothness);

            if (sourceTexels != null) RecolourPalette(finish.color);
            else runtimeMaterial.SetColor(BaseColorId, finish.color);

            Current = index;
            FinishChanged?.Invoke(index);
        }

        void RecolourPalette(Color paint)
        {
            float keyValue = Mathf.Max(paletteKey.maxColorComponent, 0.02f);
            for (int i = 0; i < sourceTexels.Length; i++)
            {
                Color texel = sourceTexels[i];
                if (Distance(texel, paletteKey) > keyTolerance)
                {
                    workingTexels[i] = sourceTexels[i];
                    continue;
                }
                // Brightness ratio rather than luma: stable for saturated keys such as pure blue.
                float shade = Mathf.Clamp(texel.maxColorComponent / keyValue, 0.6f, 1.25f);
                Color tinted = paint * shade;
                tinted.a = texel.a;
                workingTexels[i] = tinted;
            }
            runtimeTexture.SetPixels32(workingTexels);
            runtimeTexture.Apply(false);
        }

        void SwapMaterial(Renderer target)
        {
            Material[] materials = target.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != paintMaterial) continue;
                materials[i] = runtimeMaterial;
                changed = true;
            }
            if (changed) target.sharedMaterials = materials;
        }

        static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
