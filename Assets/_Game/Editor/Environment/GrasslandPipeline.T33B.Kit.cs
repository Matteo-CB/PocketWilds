using System;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T33B kit pieces that do not come from Blender: the strip's palettes, the WaterLite material and the weather
    /// kit (rain streak, rainbow arc and sun-ray card meshes are generated here, they are flat procedural shapes).
    /// </summary>
    public static partial class GrasslandPipeline
    {
        public const string WaterShader = "PocketWilds/WaterLite";
        public const string WaterMaterialPath = "Assets/_Game/Art/Materials/MAT_Water_Grassland.mat";
        public const string WeatherFolder = BakedFolder + "/Weather";
        public const string WeatherKitPath = "Assets/_Game/Content/World/DEF_Weather_Grassland.asset";

        public const string PaletteWater = "palette.grassland.water";
        public const string PaletteOldTree = "palette.grassland.old_tree";
        public const string PaletteWindmill = "palette.grassland.windmill";
        public const string PaletteWindmillSails = "palette.grassland.windmill_sails";
        public const string PaletteWell = "palette.grassland.well";
        public const string PaletteFenceWhite = "palette.grassland.fence_white";
        public const string PaletteEdgeRoots = "palette.grassland.edge_roots";
        public const string PaletteStreamStone = "palette.grassland.stream_stone";

        private static void AddStripPalettes(PaletteSetAsset set)
        {
            // Water: 0 deep, 1 shallow, 2 foam, 3 sparkle, 6 sky reflection tint.
            Add(set, PaletteWater, "3C9DBA", "86D2D3", "F3FBF8", "FFFFFF", "86D2D3", "2E7E96", "BFE6F2");
            Add(set, PaletteOldTree, "7E5A44", "6AAE52", "A6D47A", "4A8443", "5E4232", "33592F", "FFFFFF");
            Add(set, PaletteWindmill, "EDE3CE", "9C6B48", "9CC77A", "C96F55", "4A3A33", "A39478", "FFFFFF");
            Add(set, PaletteWindmillSails, "F3EBDD", "8E6446", "9CC77A", "8E6446", "8E6446", "A08A72", "FFFFFF");
            Add(set, PaletteWell, "DED3BD", "9C6B48", "9CC77A", "5FAFC8", "B9644D", "9C8E73", "FFFFFF");
            Add(set, PaletteFenceWhite, "F1ECE2", "D8CDBA", "9CC77A", "F1ECE2", "E6DCCB", "B5A993", "FFFFFF");
            Add(set, PaletteEdgeRoots, "6E5038", "6FAE57", "9ED07A", "6E5038", "6E5038", "4F3A2C", "FFFFFF");
            Add(set, PaletteStreamStone, "D6CCB6", "D6CCB6", "A8C98C", "D6CCB6", "9E9584", "8E8270", "FFFFFF");
        }

        public static Material EnsureWaterMaterial()
        {
            var shader = Shader.Find(WaterShader) ?? throw new InvalidOperationException("Shader not found: " + WaterShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_Water_Grassland" };
                AssetDatabase.CreateAsset(material, WaterMaterialPath);
            }
            material.shader = shader;
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------- weather kit ----------------

        public static WeatherKit EnsureWeatherKit()
        {
            Directory.CreateDirectory(WeatherFolder);
            var kit = AssetDatabase.LoadAssetAtPath<WeatherKit>(WeatherKitPath);
            if (kit == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(WeatherKitPath));
                kit = ScriptableObject.CreateInstance<WeatherKit>();
                AssetDatabase.CreateAsset(kit, WeatherKitPath);
            }
            kit.rainMesh = SaveProceduralMesh("MSH_RainStreak", BuildCard(0.5f, 0f, 1f));
            kit.rayMesh = SaveProceduralMesh("MSH_SunRay", BuildCard(0.5f, 0f, 1f));
            kit.rainbowMesh = SaveProceduralMesh("MSH_Rainbow", BuildArc(64, 0.86f, 1f));
            kit.rainMaterial = EnsureEffectMaterial("MAT_Rain_Grassland", "PocketWilds/RainLite", instancing: true);
            kit.rainbowMaterial = EnsureEffectMaterial("MAT_Rainbow_Grassland", "PocketWilds/Rainbow", instancing: false);
            kit.rayMaterial = EnsureEffectMaterial("MAT_SunRays_Grassland", "PocketWilds/SunRays", instancing: true);
            kit.maxDrops = 900;
            kit.rainBox = new Vector3(18f, 12f, 18f);
            kit.rainbowDistance = 55f;
            kit.rainbowRadius = 30f;
            kit.showerEveryMinutes = new Vector2(18f, 35f);
            kit.showerSeconds = new Vector2(70f, 110f);
            kit.rampSeconds = 12f;
            kit.rainbowSeconds = 50f;
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static Material EnsureEffectMaterial(string name, string shaderName, bool instancing)
        {
            string path = $"Assets/_Game/Art/Materials/{name}.mat";
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not found: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = instancing;
            // Reference values live in the shader defaults: rerunning the pipeline restores them.
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                int id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        material.SetFloat(id, shader.GetPropertyDefaultFloatValue(i));
                        break;
                    case ShaderPropertyType.Color:
                    case ShaderPropertyType.Vector:
                        material.SetVector(id, shader.GetPropertyDefaultVectorValue(i));
                        break;
                }
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A unit card: x from -halfWidth to +halfWidth, y from y0 to y1, uv over the card.</summary>
        private static Mesh BuildCard(float halfWidth, float y0, float y1)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-halfWidth, y0, 0f), new Vector3(halfWidth, y0, 0f), new Vector3(halfWidth, y1, 0f), new Vector3(-halfWidth, y1, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A half ring in the XY plane (unit outer radius), uv.x along the arc, uv.y from inner to outer.</summary>
        private static Mesh BuildArc(int segments, float inner, float outer)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float a = Mathf.PI * t;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[i * 2] = d * inner;
                vertices[i * 2 + 1] = d * outer;
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                if (i == segments)
                    continue;
                int k = i * 6, v = i * 2;
                triangles[k] = v;
                triangles[k + 1] = v + 1;
                triangles[k + 2] = v + 3;
                triangles[k + 3] = v;
                triangles[k + 4] = v + 3;
                triangles[k + 5] = v + 2;
            }
            var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveProceduralMesh(string name, Mesh source)
        {
            string path = $"{WeatherFolder}/{name}.asset";
            source.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, path);
                MakeRuntimeOnly(source);
                return source;
            }
            EditorUtility.CopySerialized(source, existing);
            existing.name = name;
            MakeRuntimeOnly(existing);
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.SaveAssetIfDirty(existing);
            Resources.UnloadAsset(existing);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
}
