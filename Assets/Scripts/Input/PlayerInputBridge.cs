using System.Collections.Generic;
using DungeonGuardians.Core;
using DungeonGuardians.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace DungeonGuardians.Input
{
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        private enum TouchZone
        {
            None,
            DPad,
            DigLeft,
            DigRight
        }

        // Mouse acts as one more pointer for testing in the editor and on PC.
        private const int MousePointerId = -1;
        // Fraction of the d-pad radius where input is ignored.
        private const float DeadZone = 0.2f;
        // tan(22.5°): splits the d-pad into 4 cardinal and 4 diagonal sectors.
        private const float DiagonalSlope = 0.4142f;

        // Each pointer keeps the zone it started in, so sliding a finger never steals another finger's control.
        private readonly Dictionary<int, TouchZone> ownedPointers = new Dictionary<int, TouchZone>();
        private readonly HashSet<int> ignoredPointers = new HashSet<int>();
        private readonly HashSet<int> activePointers = new HashSet<int>();
        private readonly List<int> stalePointers = new List<int>();

        private RectTransform dpadArea;
        private RectTransform digLeftArea;
        private RectTransform digRightArea;
        private bool ignoreHeldPointers;
        private bool restartRequested;
        private bool pauseRequested;

        public InputSnapshot HeldDirections { get; private set; }
        public bool DigLeftHeld { get; private set; }
        public bool DigRightHeld { get; private set; }

        public void BindTouchAreas(RectTransform dpad, RectTransform digLeft, RectTransform digRight)
        {
            dpadArea = dpad;
            digLeftArea = digLeft;
            digRightArea = digRight;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearHeldInput();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ClearHeldInput();
            }
        }

        public InputSnapshot Read()
        {
            InputSnapshot snapshot = PollPointers();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                snapshot.Left |= keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
                snapshot.Right |= keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
                snapshot.Up |= keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
                snapshot.Down |= keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
                snapshot.DigLeft |= keyboard.qKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame;
                snapshot.DigRight |= keyboard.eKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame;
                snapshot.Restart |= keyboard.rKey.wasPressedThisFrame;
                snapshot.Pause |= keyboard.escapeKey.wasPressedThisFrame;
                DigLeftHeld |= keyboard.qKey.isPressed || keyboard.zKey.isPressed;
                DigRightHeld |= keyboard.eKey.isPressed || keyboard.xKey.isPressed;
            }

            ApplyTiltControl(ref snapshot);

            snapshot.Restart |= restartRequested;
            snapshot.Pause |= pauseRequested;
            restartRequested = false;
            pauseRequested = false;

            // Opposite directions cancel each other out.
            if (snapshot.Left && snapshot.Right)
            {
                snapshot.Left = false;
                snapshot.Right = false;
            }

            if (snapshot.Up && snapshot.Down)
            {
                snapshot.Up = false;
                snapshot.Down = false;
                snapshot.TiltVertical = false;
            }

            HeldDirections = new InputSnapshot
            {
                Left = snapshot.Left,
                Right = snapshot.Right,
                Up = snapshot.Up,
                Down = snapshot.Down
            };
            return snapshot;
        }

        private static void ApplyTiltControl(ref InputSnapshot snapshot)
        {
            if (!GameSettings.TiltControl || snapshot.Left || snapshot.Right)
            {
                return;
            }

            Accelerometer accelerometer = Accelerometer.current;
            if (accelerometer == null)
            {
                return;
            }

            if (!accelerometer.enabled)
            {
                InputSystem.EnableDevice(accelerometer);
            }

            Vector3 acceleration = accelerometer.acceleration.ReadValue();
            float tilt = acceleration.x - GameSettings.TiltCalibration;
            float verticalTilt = acceleration.y - GameSettings.TiltVerticalCalibration;
            float deadZone = GameSettings.TiltSensitivity;
            if (tilt < -deadZone)
            {
                snapshot.Left = true;
            }
            else if (tilt > deadZone)
            {
                snapshot.Right = true;
            }

            if (!snapshot.Up && !snapshot.Down)
            {
                if (verticalTilt < -deadZone)
                {
                    snapshot.Down = true;
                    snapshot.TiltVertical = true;
                }
                else if (verticalTilt > deadZone)
                {
                    snapshot.Up = true;
                    snapshot.TiltVertical = true;
                }
            }
        }

        // Debug shortcut: keys 1-9 jump straight to that level. Returns a zero-based index or -1.
        public int ReadLevelHotkey()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        return i;
                    }
                }

                // 0 is level 10.
                if (keyboard.digit0Key.wasPressedThisFrame)
                {
                    return 9;
                }
            }
#endif
            return -1;
        }

        // Debug shortcut: Page Down / Page Up step to the next / previous level. Returns +1, -1 or 0.
        public int ReadLevelStep()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageDownKey.wasPressedThisFrame)
                {
                    return 1;
                }

                if (keyboard.pageUpKey.wasPressedThisFrame)
                {
                    return -1;
                }
            }
#endif
            return 0;
        }

        // Debug shortcut: F1 toggles Mobile Touch View with zoom in editor.
        public bool ReadToggleMobileView()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }

        // Pinch-to-zoom gesture on touchscreens, mouse wheel on PC/Editor, and +/- hotkeys.
        public float ReadPinchZoomDelta()
        {
            float delta = 0f;

            // 1. Pinch gesture with 2 or more touches
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                Touch t0 = touches[0];
                Touch t1 = touches[1];
                if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
                {
                    Vector2 cur0 = t0.screenPosition;
                    Vector2 cur1 = t1.screenPosition;
                    Vector2 prev0 = cur0 - t0.delta;
                    Vector2 prev1 = cur1 - t1.delta;
                    float curDist = Vector2.Distance(cur0, cur1);
                    float prevDist = Vector2.Distance(prev0, prev1);
                    float minDim = Mathf.Min(Screen.width, Screen.height);
                    if (minDim > 0f)
                    {
                        float diff = (curDist - prevDist) / minDim;
                        if (Mathf.Abs(diff) > 0.0005f)
                        {
                            delta += diff * 12f;
                        }
                    }
                }
            }

            // 2. Mouse scroll wheel for Editor & PC
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    delta += Mathf.Sign(scroll) * 0.75f;
                }
            }

            // 3. Keyboard zoom shortcuts (Numpad +/- and =/-)
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.numpadPlusKey.isPressed || keyboard.equalsKey.isPressed)
                {
                    delta += 3f * Time.deltaTime;
                }
                else if (keyboard.numpadMinusKey.isPressed || keyboard.minusKey.isPressed)
                {
                    delta -= 3f * Time.deltaTime;
                }
            }

            return delta;
        }

        public void Restart() => restartRequested = true;
        public void TogglePause() => pauseRequested = true;

        private InputSnapshot PollPointers()
        {
            var snapshot = new InputSnapshot();
            DigLeftHeld = false;
            DigRightHeld = false;
            activePointers.Clear();

            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    continue;
                }

                ProcessPointer(touch.touchId, touch.startScreenPosition, touch.screenPosition, ref snapshot);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                // The first frame a pointer is seen counts as its start, so the current position works for both.
                Vector2 position = mouse.position.ReadValue();
                ProcessPointer(MousePointerId, position, position, ref snapshot);
            }

            ForgetReleasedPointers();
            ignoreHeldPointers = false;
            return snapshot;
        }

        private void ProcessPointer(int id, Vector2 startPosition, Vector2 position, ref InputSnapshot snapshot)
        {
            activePointers.Add(id);
            if (ignoredPointers.Contains(id))
            {
                return;
            }

            if (!ownedPointers.TryGetValue(id, out TouchZone zone))
            {
                zone = ignoreHeldPointers ? TouchZone.None : HitTest(startPosition);
                if (zone == TouchZone.None)
                {
                    ignoredPointers.Add(id);
                    return;
                }

                ownedPointers.Add(id, zone);
                snapshot.DigLeft |= zone == TouchZone.DigLeft;
                snapshot.DigRight |= zone == TouchZone.DigRight;
            }

            switch (zone)
            {
                case TouchZone.DPad:
                    ApplyDPad(position, ref snapshot);
                    break;
                case TouchZone.DigLeft:
                    DigLeftHeld = true;
                    break;
                case TouchZone.DigRight:
                    DigRightHeld = true;
                    break;
            }
        }

        private TouchZone HitTest(Vector2 screenPosition)
        {
            if (Contains(dpadArea, screenPosition))
            {
                return TouchZone.DPad;
            }

            if (Contains(digLeftArea, screenPosition))
            {
                return TouchZone.DigLeft;
            }

            if (Contains(digRightArea, screenPosition))
            {
                return TouchZone.DigRight;
            }

            return TouchZone.None;
        }

        private void ApplyDPad(Vector2 position, ref InputSnapshot snapshot)
        {
            // Overlay canvas: RectTransform.position is already in screen pixels.
            Vector2 delta = position - (Vector2)dpadArea.position;
            float radius = dpadArea.rect.width * 0.5f * dpadArea.lossyScale.x;
            float deadZone = radius * DeadZone;
            if (delta.sqrMagnitude < deadZone * deadZone)
            {
                return;
            }

            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);

            // Diagonal sectors report both axes; the simulation picks the axis by context (ladder or not).
            if (absX > absY * DiagonalSlope)
            {
                snapshot.Left |= delta.x < 0f;
                snapshot.Right |= delta.x > 0f;
            }

            if (absY > absX * DiagonalSlope)
            {
                snapshot.Down |= delta.y < 0f;
                snapshot.Up |= delta.y > 0f;
            }
        }

        private void ForgetReleasedPointers()
        {
            stalePointers.Clear();
            foreach (int id in ownedPointers.Keys)
            {
                if (!activePointers.Contains(id))
                {
                    stalePointers.Add(id);
                }
            }

            foreach (int id in ignoredPointers)
            {
                if (!activePointers.Contains(id))
                {
                    stalePointers.Add(id);
                }
            }

            foreach (int id in stalePointers)
            {
                ownedPointers.Remove(id);
                ignoredPointers.Remove(id);
            }
        }

        private void ClearHeldInput()
        {
            // Fingers still on the screen after focus returns stay ignored until they are lifted.
            ownedPointers.Clear();
            ignoreHeldPointers = true;
            restartRequested = false;
            pauseRequested = false;
            HeldDirections = InputSnapshot.Empty;
            DigLeftHeld = false;
            DigRightHeld = false;
        }

        private static bool Contains(RectTransform area, Vector2 screenPosition)
        {
            // Hidden controls (on a PC, see GameSettings) take no input.
            return area != null && area.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(area, screenPosition, null);
        }
    }
}
