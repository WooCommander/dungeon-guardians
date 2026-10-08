using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DungeonGuardians.Editor
{
    // Automatically creates and configures the Universal Render Pipeline (URP) Asset
    // to resolve Unity 6 Built-in Pipeline deprecation warnings.
    [InitializeOnLoad]
    public static class SetupURP
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelineAssetPath = "Assets/Settings/UniversalRenderPipelineAsset.asset";
        private const string RendererDataPath = "Assets/Settings/UniversalRendererData.asset";

        static SetupURP()
        {
            EditorApplication.delayCall += EnsureURPConfigured;
        }

        [MenuItem("Dungeon Guardians/Настроить URP (Fix Deprecated Pipeline)")]
        public static void EnsureURPConfigured()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererDataPath);
                }

                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                pipelineAsset.supportsHDR = false;
                pipelineAsset.shadowDistance = 25f;
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
                AssetDatabase.SaveAssets();
            }

            bool modified = false;

            if (GraphicsSettings.defaultRenderPipeline != pipelineAsset)
            {
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                modified = true;
            }

            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != pipelineAsset)
                {
                    QualitySettings.renderPipeline = pipelineAsset;
                    modified = true;
                }
            }
            QualitySettings.SetQualityLevel(currentLevel, false);

            if (modified)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[URP Setup] Universal Render Pipeline Asset успешно привязан в GraphicsSettings и QualitySettings!");
            }
        }
    }
}
