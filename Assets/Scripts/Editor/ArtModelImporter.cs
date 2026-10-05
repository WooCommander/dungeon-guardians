using UnityEditor;

namespace DungeonGuardians.Editor
{
    // Import settings for the FBX files produced by the scripts in ArtSource.
    public sealed class ArtModelImporter : AssetPostprocessor
    {
        private const string CharacterFolder = "Assets/Resources/Characters/";
        private const string EnvironmentFolder = "Assets/Resources/Environment/";

        // Tripo maps (Textures/<model>_basecolor, _normal) are bound to materials at runtime by ModelTextures.
        // Source maps are up to 4K, far more than a phone needs: a character is smaller than one cell, a block half a cell.
        private const int CharacterTextureSize = 1024;
        private const int EnvironmentTextureSize = 512;

        // Bump whenever the rules below change: Unity then reimports every model and texture this postprocessor touches.
        public override uint GetVersion()
        {
            return 4;
        }

        private void OnPreprocessTexture()
        {
            bool character = assetPath.StartsWith(CharacterFolder);
            if (!character && !assetPath.StartsWith(EnvironmentFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = character ? CharacterTextureSize : EnvironmentTextureSize;
            if (assetPath.ToLowerInvariant().Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
        }

        private void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(CharacterFolder);
            bool environment = assetPath.StartsWith(EnvironmentFolder);
            if (!character && !environment)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;

            if (character)
            {
                // CharacterView plays clips through the legacy Animation component, so no Animator Controller is needed.
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                // CharacterView bakes the skin once to measure the model; that needs readable meshes in a build.
                importer.isReadable = true;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }
    }
}
