using UnityEditor;

namespace DungeonGuardians.Editor
{
    // Import settings for the FBX files produced by ArtSource/convert_to_fbx.py.
    public sealed class ArtModelImporter : AssetPostprocessor
    {
        private const string CharacterFolder = "Assets/Resources/Characters/";
        private const string EnvironmentFolder = "Assets/Resources/Environment/";

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
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }
    }
}
