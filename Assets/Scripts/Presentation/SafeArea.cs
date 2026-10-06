using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Keeps a full-screen UI root inside the device's safe area (TZ section 9): on phones with a camera cutout or
    // rounded corners the HUD and touch controls stay clear of them. On a PC the safe area is the whole screen.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform area;
        private Rect applied;
        private Vector2Int screen;

        // Moves all current children of canvas into a new safe-area root.
        public static SafeArea Wrap(Transform canvas)
        {
            var root = new GameObject("Safe Area", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Transform child = canvas.GetChild(i);
                if (child != rect)
                {
                    child.SetParent(rect, false);
                    child.SetAsFirstSibling();
                }
            }

            var safe = root.AddComponent<SafeArea>();
            safe.Apply();
            return safe;
        }

        private void Awake()
        {
            area = (RectTransform)transform;
        }

        private void Update()
        {
            if (Screen.safeArea != applied || Screen.width != screen.x || Screen.height != screen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (area == null)
            {
                area = (RectTransform)transform;
            }

            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            area.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            area.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
        }
    }
}
