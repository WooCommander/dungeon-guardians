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
            EnsureCamera();
            EnsureLighting();

            var input = gameObject.AddComponent<PlayerInputBridge>();
            var renderer = gameObject.AddComponent<LevelRenderer>();
            var hud = gameObject.AddComponent<GameHud>();

            controller = gameObject.AddComponent<GameController>();
            controller.Initialize(input, renderer, hud, new BalanceConfig(), new ProgressStore());
        }

        // Character models use lit materials; tiles are unlit sprites and are not affected.
        private static void EnsureLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f);

            if (FindObjectOfType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.9f, 0.76f);
            light.intensity = 1.2f;
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
