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

            var input = gameObject.AddComponent<PlayerInputBridge>();
            var renderer = gameObject.AddComponent<LevelRenderer>();
            var hud = gameObject.AddComponent<GameHud>();

            controller = gameObject.AddComponent<GameController>();
            controller.Initialize(input, renderer, hud, new BalanceConfig(), new ProgressStore());
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
            camera.backgroundColor = new Color(0.05f, 0.09f, 0.11f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
