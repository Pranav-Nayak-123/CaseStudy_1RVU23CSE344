using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CarDesigner
{
    /// <summary>Synchronous UI hit test, so gestures that start on a panel never reach the 3D view.</summary>
    public static class UiHit
    {
        static readonly List<RaycastResult> Results = new List<RaycastResult>();
        static PointerEventData pointer;
        static EventSystem pointerOwner;

        public static bool IsOver(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (pointerOwner != eventSystem)
            {
                pointer = new PointerEventData(eventSystem);
                pointerOwner = eventSystem;
            }

            pointer.position = screenPosition;
            Results.Clear();
            eventSystem.RaycastAll(pointer, Results);
            return Results.Count > 0;
        }
    }
}
