using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarDesigner.EditorTools
{
    /// <summary>
    /// Background for the studio scene: a rooftop parking deck at dusk, surrounded by a city skyline.
    /// Everything is generated from code (meshes, textures, materials), so no extra art assets are needed.
    /// The car turntable stays at the centre of the deck; only the surroundings change.
    /// </summary>
    static class CityGarageEnvironment
    {
        const string Root = StudioSceneBuilder.Root;
        const string MaterialsFolder = Root + "/Materials";
        const string SettingsFolder = Root + "/Settings";

        // Deck: 44 m × 44 m, top at y = 0. The orbit camera never goes further than 14 m from the car.
        const float DeckHalf = 22f;
        const float CityGround = -45f;

        public static readonly Color Haze = new Color32(0x4C, 0x52, 0x68, 0xFF);

        public static readonly Color AmbientSky = new Color32(0x6E, 0x78, 0x96, 0xFF);
        public static readonly Color AmbientEquator = new Color32(0x86, 0x6E, 0x62, 0xFF);
        public static readonly Color AmbientGround = new Color32(0x2A, 0x2A, 0x2E, 0xFF);

        struct Mats
        {
            public Material Concrete, Slab, Parapet, LineWhite, LineYellow, Pole, Lamp, Core, CoreGlow, Facade, CityGround;
        }

        // ───────────────────────────── sky, fog, reflections ─────────────────────────────

        public static Material SkyMaterial()
        {
            Material sky = StudioSceneBuilder.LoadOrCreate("CityDuskSky", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.15f);
            sky.SetColor("_SkyTint", new Color32(0x86, 0x7A, 0xA8, 0xFF));
            sky.SetColor("_GroundColor", new Color32(0x26, 0x27, 0x2E, 0xFF));
            sky.SetFloat("_Exposure", 0.55f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>Dusk-city reflection: blue-violet zenith, warm horizon band, dark skyline, concrete floor.</summary>
        public static Cubemap Reflection()
        {
            const int size = 32;
            string path = SettingsFolder + "/CityReflection.cubemap";
            AssetDatabase.DeleteAsset(path);

            var cubemap = new Cubemap(size, TextureFormat.RGBA32, false) { name = "CityReflection" };
            Color zenith = new Color32(0x4A, 0x58, 0x86, 0xFF);
            Color horizon = new Color32(0xE6, 0xA0, 0x72, 0xFF);
            Color skyline = new Color32(0x26, 0x28, 0x32, 0xFF);
            Color window = new Color32(0xF2, 0xC8, 0x8A, 0xFF);
            Color floor = new Color32(0x3C, 0x3C, 0x40, 0xFF);

            var pixels = new Color[size * size];
            var random = new System.Random(11);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                // Each column has a building of random height; above it the sky fades from horizon to zenith.
                var heights = new float[size];
                for (int x = 0; x < size; x++) heights[x] = 0.5f + (float)random.NextDouble() * 0.18f;
                for (int y = 0; y < size; y++)
                {
                    float t = 1f - y / (size - 1f); // 0 = bottom, 1 = top
                    for (int x = 0; x < size; x++)
                    {
                        Color c;
                        if (t < 0.5f) c = floor;
                        else if (t < heights[x]) c = random.NextDouble() < 0.12 ? window : skyline;
                        else c = Color.Lerp(horizon, zenith, Mathf.SmoothStep(heights[x], 1f, t));
                        pixels[y * size + x] = c;
                    }
                }
                cubemap.SetPixels(pixels, face);
            }
            for (int i = 0; i < pixels.Length; i++) pixels[i] = zenith;
            cubemap.SetPixels(pixels, CubemapFace.PositiveY);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = floor;
            cubemap.SetPixels(pixels, CubemapFace.NegativeY);
            cubemap.Apply(false);

            AssetDatabase.CreateAsset(cubemap, path);
            return cubemap;
        }

        // ───────────────────────────── scene content ─────────────────────────────

        /// <param name="turntable">Turntable material from the studio builder (kept unchanged).</param>
        public static void Build(Material turntable, Material turntableRim, float turntableTop, Mesh turntableDisc, Mesh turntableRing,
            GameObject[] parkedCars)
        {
            Mats m = CreateMaterials();
            var environment = new GameObject("City Garage Environment");
            Transform root = environment.transform;

            // Deck floor (visual only; no collider so it never blocks part picking).
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Deck Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localScale = new Vector3(DeckHalf / 5f, 1f, DeckHalf / 5f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = m.Concrete;
            floor.isStatic = true;

            Box("Deck Slab", root, m.Slab, new Vector3(0f, -0.62f, 0f), new Vector3(DeckHalf * 2f + 0.6f, 1.2f, DeckHalf * 2f + 0.6f), ShadowCastingMode.Off);

            // Turntable display platform stays exactly as in the studio.
            StudioSceneBuilder.MeshObject("Turntable", root, turntable, turntableDisc, ShadowCastingMode.On);
            StudioSceneBuilder.MeshObject("Turntable Rim", root, turntableRim, turntableRing, ShadowCastingMode.Off);

            BuildMarkings(root, m);
            BuildParapet(root, m);
            BuildLampPosts(root, m);
            BuildCore(root, m);
            ParkCars(root, parkedCars);
            BuildSkyline(root, m);
        }

        static Mats CreateMaterials()
        {
            var m = new Mats();
            Texture2D concreteTex = NoiseTexture("ConcreteNoise", 256, new Color32(0x58, 0x58, 0x5C, 0xFF), 0.10f, 5);
            m.Concrete = StudioSceneBuilder.Lit("DeckConcrete", Color.white, 0.22f, 0f, concreteTex);
            m.Concrete.SetTextureScale("_BaseMap", new Vector2(9f, 9f));
            m.Slab = StudioSceneBuilder.Lit("DeckSlab", new Color32(0x55, 0x55, 0x58, 0xFF), 0.1f, 0f, concreteTex);
            m.Parapet = StudioSceneBuilder.Lit("Parapet", new Color32(0x86, 0x84, 0x80, 0xFF), 0.15f, 0f, concreteTex);
            m.LineWhite = StudioSceneBuilder.Lit("LineWhite", new Color32(0xE4, 0xE4, 0xE0, 0xFF), 0.35f);
            m.LineYellow = StudioSceneBuilder.Lit("LineYellow", new Color32(0xE3, 0xB2, 0x3B, 0xFF), 0.35f);
            m.Pole = StudioSceneBuilder.Lit("LampPole", new Color32(0x3A, 0x3C, 0x42, 0xFF), 0.3f);
            m.Lamp = Emissive("LampHead", new Color32(0xFF, 0xE6, 0xB8, 0xFF), 4.5f, null);
            m.Core = StudioSceneBuilder.Lit("StairCore", new Color32(0x7A, 0x78, 0x74, 0xFF), 0.15f, 0f, concreteTex);
            m.CoreGlow = Emissive("StairCoreGlow", new Color32(0x9F, 0xE8, 0xC8, 0xFF), 2.2f, null);

            Texture2D facade = FacadeTexture(out Texture2D lit);
            m.Facade = Emissive("CityFacade", Color.white, 1.7f, lit);
            m.Facade.SetTexture("_BaseMap", facade);
            m.Facade.SetColor("_BaseColor", Color.white);
            m.Facade.SetFloat("_Smoothness", 0.35f);
            m.CityGround = StudioSceneBuilder.Lit("CityGround", new Color32(0x1C, 0x1D, 0x22, 0xFF), 0.2f);
            foreach (Material material in new[] { m.Concrete, m.Facade }) EditorUtility.SetDirty(material);
            return m;
        }

        static Material Emissive(string name, Color color, float intensity, Texture emissionMap)
        {
            Material material = StudioSceneBuilder.Lit(name, emissionMap != null ? Color.white : color * 0.6f, 0.4f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_EmissionColor", (emissionMap != null ? Color.white : color) * intensity);
            material.SetTexture("_EmissionMap", emissionMap);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ── deck details ──

        static void BuildMarkings(Transform root, Mats m)
        {
            var markings = new GameObject("Parking Markings").transform;
            markings.SetParent(root, false);
            const float y = 0.003f, bayWidth = 2.7f, bayDepth = 5.4f, line = 0.12f;

            // Two rows of bays on the left and right of the display area, cars nose-in towards the parapet.
            foreach (float side in new[] { -1f, 1f })
            {
                float inner = side * (DeckHalf - 0.6f - bayDepth);
                float outer = side * (DeckHalf - 0.6f);
                for (int i = 0; i <= 8; i++)
                {
                    float z = -bayWidth * 4f + i * bayWidth;
                    Box("Bay Line", markings, m.LineWhite, new Vector3((inner + outer) * 0.5f, y, z), new Vector3(bayDepth, 0.006f, line), ShadowCastingMode.Off);
                }
                Box("Bay End", markings, m.LineWhite, new Vector3(inner, y, 0f), new Vector3(line, 0.006f, bayWidth * 8f), ShadowCastingMode.Off);
            }

            // Yellow safety ring around the turntable and a walkway to the stair core.
            const int dashes = 36;
            for (int i = 0; i < dashes; i++)
            {
                float a = i * Mathf.PI * 2f / dashes;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.4f;
                Box("Safety Dash", markings, m.LineYellow, p + Vector3.up * y, new Vector3(0.12f, 0.006f, 0.42f), ShadowCastingMode.Off)
                    .transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, p.normalized));
            }
            for (int i = 0; i < 7; i++)
                Box("Walkway Stripe", markings, m.LineYellow, new Vector3(-DeckHalf + 3.4f + i * 0.4f, y, DeckHalf - 8.4f), new Vector3(0.22f, 0.006f, 3.2f), ShadowCastingMode.Off);
        }

        static void BuildParapet(Transform root, Mats m)
        {
            var parapet = new GameObject("Parapet").transform;
            parapet.SetParent(root, false);
            const float height = 1.1f, thickness = 0.35f;
            float edge = DeckHalf - thickness * 0.5f;
            Box("Parapet N", parapet, m.Parapet, new Vector3(0f, height * 0.5f, edge), new Vector3(DeckHalf * 2f, height, thickness), ShadowCastingMode.On);
            Box("Parapet S", parapet, m.Parapet, new Vector3(0f, height * 0.5f, -edge), new Vector3(DeckHalf * 2f, height, thickness), ShadowCastingMode.On);
            Box("Parapet E", parapet, m.Parapet, new Vector3(edge, height * 0.5f, 0f), new Vector3(thickness, height, DeckHalf * 2f), ShadowCastingMode.On);
            Box("Parapet W", parapet, m.Parapet, new Vector3(-edge, height * 0.5f, 0f), new Vector3(thickness, height, DeckHalf * 2f), ShadowCastingMode.On);
            // Steel rail on top.
            float rail = DeckHalf - thickness * 0.5f;
            Box("Rail N", parapet, m.Pole, new Vector3(0f, height + 0.25f, rail), new Vector3(DeckHalf * 2f, 0.06f, 0.06f), ShadowCastingMode.Off);
            Box("Rail S", parapet, m.Pole, new Vector3(0f, height + 0.25f, -rail), new Vector3(DeckHalf * 2f, 0.06f, 0.06f), ShadowCastingMode.Off);
            Box("Rail E", parapet, m.Pole, new Vector3(rail, height + 0.25f, 0f), new Vector3(0.06f, 0.06f, DeckHalf * 2f), ShadowCastingMode.Off);
            Box("Rail W", parapet, m.Pole, new Vector3(-rail, height + 0.25f, 0f), new Vector3(0.06f, 0.06f, DeckHalf * 2f), ShadowCastingMode.Off);
        }

        static void BuildLampPosts(Transform root, Mats m)
        {
            var lamps = new GameObject("Lamp Posts").transform;
            lamps.SetParent(root, false);
            float d = DeckHalf - 1.2f;
            var spots = new[]
            {
                new Vector3(d, 0f, d), new Vector3(d, 0f, -d), new Vector3(-d, 0f, -d),
                new Vector3(0f, 0f, -d), new Vector3(d, 0f, 0f), new Vector3(-d, 0f, 0f)
            };
            foreach (Vector3 p in spots)
            {
                Box("Pole", lamps, m.Pole, p + Vector3.up * 3.2f, new Vector3(0.16f, 6.4f, 0.16f), ShadowCastingMode.On);
                Vector3 inward = -new Vector3(p.x, 0f, p.z).normalized;
                Box("Arm", lamps, m.Pole, p + Vector3.up * 6.35f + inward * 0.6f, new Vector3(0.1f, 0.1f, 1.3f), ShadowCastingMode.Off)
                    .transform.rotation = Quaternion.LookRotation(inward);
                Box("Lamp Head", lamps, m.Lamp, p + Vector3.up * 6.25f + inward * 1.15f, new Vector3(0.55f, 0.1f, 0.3f), ShadowCastingMode.Off)
                    .transform.rotation = Quaternion.LookRotation(inward);
            }
        }

        static void BuildCore(Transform root, Mats m)
        {
            // Stair / lift core in the far corner with a glowing doorway and roof sign strip.
            var core = new GameObject("Stair Core").transform;
            core.SetParent(root, false);
            var centre = new Vector3(-DeckHalf + 4.6f, 0f, DeckHalf - 3.6f);
            Box("Core", core, m.Core, centre + Vector3.up * 2.4f, new Vector3(7f, 4.8f, 5f), ShadowCastingMode.On);
            Box("Core Roof", core, m.Slab, centre + Vector3.up * 4.9f, new Vector3(7.4f, 0.2f, 5.4f), ShadowCastingMode.On);
            Box("Doorway", core, m.CoreGlow, centre + new Vector3(0f, 1.15f, -2.51f), new Vector3(1.6f, 2.3f, 0.02f), ShadowCastingMode.Off);
            Box("Sign Strip", core, m.Lamp, centre + new Vector3(0f, 4.4f, -2.52f), new Vector3(5.6f, 0.18f, 0.02f), ShadowCastingMode.Off);
        }

        static void ParkCars(Transform root, GameObject[] cars)
        {
            if (cars == null) return;
            var parking = new GameObject("Parked Cars").transform;
            parking.SetParent(root, false);
            const float bayWidth = 2.7f, bayDepth = 5.4f;
            // (side, bay index) for each parked car: a few occupied bays, the rest left empty.
            var bays = new[] { (1f, 1), (1f, 2), (1f, 5), (-1f, 2), (-1f, 6), (-1f, 3) };
            for (int i = 0; i < cars.Length && i < bays.Length; i++)
            {
                GameObject car = cars[i];
                if (car == null) continue;
                car.transform.SetParent(parking, true);
                (float side, int bay) = bays[i];
                float x = side * (DeckHalf - 0.6f - bayDepth * 0.5f);
                float z = -bayWidth * 4f + (bay + 0.5f) * bayWidth;

                Bounds b = WorldBounds(car);
                if (b.size.z > b.size.x) car.transform.Rotate(0f, 90f, 0f, Space.World);
                if (i % 2 == 1) car.transform.Rotate(0f, 180f, 0f, Space.World);
                b = WorldBounds(car);
                car.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z);
                foreach (Collider c in car.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (Renderer r in car.GetComponentsInChildren<Renderer>()) r.gameObject.isStatic = true;
            }
        }

        // ── skyline ──

        static void BuildSkyline(Transform root, Mats m)
        {
            var random = new System.Random(2026);
            var combine = new List<CombineInstance>();
            const float cell = 17f;
            const int extent = 10;
            for (int gx = -extent; gx <= extent; gx++)
            for (int gz = -extent; gz <= extent; gz++)
            {
                var centre = new Vector2(gx * cell, gz * cell);
                float distance = centre.magnitude;
                if (distance < 38f || distance > 175f) continue;
                if (random.NextDouble() < 0.2) continue;

                float w = 8f + (float)random.NextDouble() * 7f;
                float d = 8f + (float)random.NextDouble() * 7f;
                // Roof height relative to the deck: nearby blocks sit at or below the deck (so the sky stays open
                // behind the car); further out the towers rise, giving a skyline against the sunset.
                float near = Mathf.InverseLerp(38f, 175f, distance);
                float tall = (float)random.NextDouble();
                float roofAboveDeck = Mathf.Lerp(-16f, 6f, near) + tall * tall * Mathf.Lerp(14f, 75f, near);
                float h = roofAboveDeck - CityGround;
                var jitter = new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 4f;
                var position = new Vector3(centre.x + jitter.x, CityGround, centre.y + jitter.y);
                var uvOffset = new Vector2((float)random.Next(0, 8) / 8f, (float)random.Next(0, 4) / 8f);

                combine.Add(new CombineInstance
                {
                    mesh = BuildingMesh(new Vector3(w, h, d), uvOffset),
                    transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f), Vector3.one)
                });

                // Occasional set-back tower on top of tall blocks.
                if (roofAboveDeck > 30f)
                {
                    float w2 = w * 0.6f, d2 = d * 0.6f, h2 = 8f + (float)random.NextDouble() * 14f;
                    combine.Add(new CombineInstance
                    {
                        mesh = BuildingMesh(new Vector3(w2, h2, d2), uvOffset),
                        transform = Matrix4x4.TRS(position + Vector3.up * h, Quaternion.identity, Vector3.one)
                    });
                }
            }

            var skyline = new Mesh { indexFormat = IndexFormat.UInt32 };
            skyline.CombineMeshes(combine.ToArray(), true, true);
            skyline.RecalculateBounds();
            foreach (CombineInstance instance in combine) Object.DestroyImmediate(instance.mesh);
            StudioSceneBuilder.MeshObject("City Skyline", root, m.Facade, StudioSceneBuilder.SaveMesh("CitySkyline", skyline), ShadowCastingMode.Off);

            Box("City Ground", root, m.CityGround, new Vector3(0f, CityGround, 0f), new Vector3(420f, 0.5f, 420f), ShadowCastingMode.Off);
        }

        /// <summary>Box with UVs in "window units" so every building shows the same window size.</summary>
        static Mesh BuildingMesh(Vector3 size, Vector2 uvOffset)
        {
            const float tileWidth = 24f, tileHeight = 28f; // one texture tile = 8 windows × 8 floors
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float hx = size.x * 0.5f, hz = size.z * 0.5f, h = size.y;

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float width, bool roof)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { n, n, n, n });
                if (roof)
                    uvs.AddRange(new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
                else
                    uvs.AddRange(new[]
                    {
                        uvOffset + new Vector2(0f, 0f), uvOffset + new Vector2(width / tileWidth, 0f),
                        uvOffset + new Vector2(width / tileWidth, h / tileHeight), uvOffset + new Vector2(0f, h / tileHeight)
                    });
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            Quad(new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz), Vector3.back, size.x, false);
            Quad(new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz), new Vector3(-hx, h, hz), new Vector3(hx, h, hz), Vector3.forward, size.x, false);
            Quad(new Vector3(-hx, 0, hz), new Vector3(-hx, 0, -hz), new Vector3(-hx, h, -hz), new Vector3(-hx, h, hz), Vector3.left, size.z, false);
            Quad(new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), Vector3.right, size.z, false);
            Quad(new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz), new Vector3(hx, h, hz), new Vector3(-hx, h, hz), Vector3.up, 0f, true);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        // ── generated textures ──

        /// <summary>
        /// One tile = 8 × 8 windows. Base map: dark facade, bluish glass. Emission map: about a third of the windows lit
        /// (warm or cool). Texel (0, 0) sits on a mullion so roofs (UV 0, 0) stay dark.
        /// </summary>
        static Texture2D FacadeTexture(out Texture2D emission)
        {
            const int size = 256, cells = 8, cellPx = size / cells;
            var random = new System.Random(7);
            var basePixels = new Color32[size * size];
            var glowPixels = new Color32[size * size];
            var wall = new Color32(0x2E, 0x30, 0x38, 0xFF);
            var glass = new Color32(0x3A, 0x48, 0x60, 0xFF);
            var warm = new Color32(0xFF, 0xD2, 0x8C, 0xFF);
            var cool = new Color32(0xB8, 0xD6, 0xFF, 0xFF);
            var off = new Color32(0, 0, 0, 0xFF);

            for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
            {
                double roll = random.NextDouble();
                Color32 glow = roll < 0.24 ? warm : roll < 0.34 ? cool : off;
                float dim = 0.55f + (float)random.NextDouble() * 0.45f;
                glow = new Color32((byte)(glow.r * dim), (byte)(glow.g * dim), (byte)(glow.b * dim), 0xFF);
                for (int y = 0; y < cellPx; y++)
                for (int x = 0; x < cellPx; x++)
                {
                    bool isWindow = x >= 5 && x < cellPx - 5 && y >= 7 && y < cellPx - 6;
                    int i = (cy * cellPx + y) * size + cx * cellPx + x;
                    basePixels[i] = isWindow ? glass : wall;
                    glowPixels[i] = isWindow ? glow : off;
                }
            }

            Texture2D Save(string name, Color32[] pixels)
            {
                string path = $"{SettingsFolder}/{name}.asset";
                AssetDatabase.DeleteAsset(path);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
                texture.SetPixels32(pixels);
                texture.Apply(true);
                AssetDatabase.CreateAsset(texture, path);
                return texture;
            }

            emission = Save("CityFacadeGlow", glowPixels);
            return Save("CityFacade", basePixels);
        }

        static Texture2D NoiseTexture(string name, int size, Color32 tint, float strength, int seed)
        {
            string path = $"{SettingsFolder}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var random = new System.Random(seed);
            var pixels = new Color32[size * size];
            float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Tileable blotches (sin-based) plus fine grain.
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float blotch = Mathf.Sin(u * 3f + ox) * Mathf.Sin(v * 2f + oy) * 0.5f + Mathf.Sin(u * 7f + v * 5f) * 0.25f;
                float grain = (float)random.NextDouble() - 0.5f;
                float k = 1f + (blotch * 0.6f + grain) * strength;
                pixels[y * size + x] = new Color32((byte)Mathf.Clamp(tint.r * k, 0, 255), (byte)Mathf.Clamp(tint.g * k, 0, 255),
                    (byte)Mathf.Clamp(tint.b * k, 0, 255), 0xFF);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        // ── helpers ──

        static GameObject Box(string name, Transform parent, Material material, Vector3 position, Vector3 size, ShadowCastingMode shadows)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            box.isStatic = true;
            return box;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
