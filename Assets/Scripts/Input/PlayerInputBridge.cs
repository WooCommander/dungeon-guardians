using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Input
{
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        private InputSnapshot touchState;
        private InputSnapshot pointerState;
        private bool restartRequested;
        private bool pauseRequested;

        private void Update()
        {
            pointerState = InputSnapshot.Empty;
            if (!UnityEngine.Input.GetMouseButton(0))
            {
                return;
            }

            Vector2 position = UnityEngine.Input.mousePosition;
            float width = Screen.width;
            float height = Screen.height;

            if (position.y > height * 0.42f)
            {
                return;
            }

            if (position.x < width * 0.36f)
            {
                Vector2 center = new Vector2(width * 0.14f, height * 0.15f);
                Vector2 delta = position - center;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    pointerState.Left = delta.x < -20f;
                    pointerState.Right = delta.x > 20f;
                }
                else
                {
                    pointerState.Down = delta.y < -20f;
                    pointerState.Up = delta.y > 20f;
                }
            }
            else if (position.x > width * 0.64f)
            {
                pointerState.DigLeft = position.x < width * 0.82f;
                pointerState.DigRight = position.x >= width * 0.82f;
            }
        }

        public InputSnapshot Read()
        {
            InputSnapshot snapshot = touchState;
            snapshot.Left |= pointerState.Left;
            snapshot.Right |= pointerState.Right;
            snapshot.Up |= pointerState.Up;
            snapshot.Down |= pointerState.Down;
            snapshot.DigLeft |= pointerState.DigLeft;
            snapshot.DigRight |= pointerState.DigRight;
            snapshot.Left |= UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.A);
            snapshot.Right |= UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D);
            snapshot.Up |= UnityEngine.Input.GetKey(KeyCode.UpArrow) || UnityEngine.Input.GetKey(KeyCode.W);
            snapshot.Down |= UnityEngine.Input.GetKey(KeyCode.DownArrow) || UnityEngine.Input.GetKey(KeyCode.S);
            snapshot.DigLeft |= UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.Z);
            snapshot.DigRight |= UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.X);
            snapshot.Restart |= restartRequested || UnityEngine.Input.GetKeyDown(KeyCode.R);
            snapshot.Pause |= pauseRequested || UnityEngine.Input.GetKeyDown(KeyCode.Escape);

            restartRequested = false;
            pauseRequested = false;
            touchState.DigLeft = false;
            touchState.DigRight = false;
            return snapshot;
        }

        public void SetLeft(bool value) => touchState.Left = value;
        public void SetRight(bool value) => touchState.Right = value;
        public void SetUp(bool value) => touchState.Up = value;
        public void SetDown(bool value) => touchState.Down = value;
        public void DigLeft() => touchState.DigLeft = true;
        public void DigRight() => touchState.DigRight = true;
        public void Restart() => restartRequested = true;
        public void TogglePause() => pauseRequested = true;
    }
}
