using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonGuardians.Presentation
{
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> OnChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnChanged?.Invoke(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnChanged?.Invoke(false);
        }
    }
}
