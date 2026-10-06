using DungeonGuardians.Core;
using DungeonGuardians.Input;
using DungeonGuardians.Persistence;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private GameController controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindObjectOfType<GameBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("Dungeon Guardians");
            root.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // Landscape only (TZ section 9), turning with the phone between its two landscape sides.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            EnsureCamera();
            EnsureLighting();

            var input = gameObject.AddComponent<PlayerInputBridge>();
            var renderer = gameObject.AddComponent<LevelRenderer>();
            var hud = gameObject.AddComponent<GameHud>();
            var menu = gameObject.AddComponent<GameMenu>();
            var music = gameObject.AddComponent<MusicPlayer>();

            controller = gameObject.AddComponent<GameController>();
            controller.Initialize(input, renderer, hud, menu, music, new BalanceConfig(), new ProgressStore());
        }

        // Blocks and characters use lit materials; sprites (gold, flames, glows, the painting) are unlit. A warm golden
        // key light and warm ambient turn the sandstone the saturated orange of the concept screen (about 160, 95, 40
        // on a lit block face) against the cool teal painting; the torches add brighter pools nearby.
        private static void EnsureLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.25f, 0.2f);

            // A scene may bring its own directional light (Unity's default is bright and from behind): it is reused
            // with the game's settings, so the dungeon is equally dim in any scene.
            Light light = null;
            foreach (Light candidate in FindObjectsOfType<Light>())
            {
                if (candidate.type == LightType.Directional)
                {
                    light = candidate;
                    break;
                }
            }

            if (light == null)
            {
                light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            GameObject lightObject = light.gameObject;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            // From the upper left and in front, so faces turned towards the camera are lit.
            lightObject.transform.rotation = Quaternion.Euler(35f, 25f, 0f);
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.12f, 0.15f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
