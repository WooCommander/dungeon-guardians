using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Tripo models ship their maps as Resources/<folder>/Textures/<model>_basecolor and <model>_normal.
    // The textures are bound here at runtime rather than during import, so the result never depends on asset import order.
    public static class ModelTextures
    {
        // Tripo keeps its own material name in the export; other materials (e.g. the explorer's pickaxe) are left alone.
        private const string TripoMaterialPrefix = "tripo_mat";

        // One textured material per model, shared by all its instances so identical pieces can be instanced together.
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static void Apply(GameObject instance, string folder, string model, bool instancing)
        {
            string key = folder + "/" + model;
            if (!Materials.TryGetValue(key, out Material textured))
            {
                textured = Build(instance, folder, model, instancing);
                Materials[key] = textured;
            }

            if (textured == null)
            {
                return;
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (IsTripoMaterial(materials[i]))
                    {
                        materials[i] = textured;
                    }
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material Build(GameObject instance, string folder, string model, bool instancing)
        {
            var baseColor = Resources.Load<Texture2D>($"{folder}/Textures/{model}_basecolor");
            if (baseColor == null)
            {
                // Procedural models have no maps and keep their imported material colours.
                return null;
            }

            Material source = null;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (source == null && IsTripoMaterial(material))
                    {
                        source = material;
                    }
                }
            }

            if (source == null)
            {
                Debug.LogWarning($"{folder}/{model}: textures found but no {TripoMaterialPrefix}* material to apply them to.");
                return null;
            }

            var textured = new Material(source)
            {
                name = model + " (textured)",
                color = Color.white,
                enableInstancing = instancing
            };
            SetTexture(textured, baseColor, "_MainTex", "_BaseMap");

            var normal = Resources.Load<Texture2D>($"{folder}/Textures/{model}_normal");
            if (normal != null)
            {
                SetTexture(textured, normal, "_BumpMap");
                textured.EnableKeyword("_NORMALMAP");
            }

            // Metallic/roughness maps are not shipped: a matte, non-metal surface suits the painted look.
            SetFloat(textured, 0f, "_Metallic");
            SetFloat(textured, 0.2f, "_Glossiness", "_Smoothness");
            return textured;
        }

        private static bool IsTripoMaterial(Material material)
        {
            return material != null && material.name.StartsWith(TripoMaterialPrefix);
        }

        // Property names differ between the built-in Standard shader and URP Lit.
        private static void SetTexture(Material material, Texture texture, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    material.SetTexture(property, texture);
                }
            }
        }

        private static void SetFloat(Material material, float value, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, value);
                }
            }
        }
    }
}
