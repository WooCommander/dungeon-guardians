using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Input
{
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        private InputSnapshot touchState;
        private bool restartRequested;
        private bool pauseRequested;

        public InputSnapshot Read()
        {
            InputSnapshot snapshot = touchState;
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
