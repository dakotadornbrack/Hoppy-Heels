using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A button that reports whether it's being held, for the on-screen move buttons.
/// (Unity's Button only fires when released, which doesn't work for holding.)
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    int pointersDown;

    public bool IsHeld => pointersDown > 0;

    /// <summary>When this button was last pressed; used so the newest press wins if both are held.</summary>
    public float LastPressTime { get; private set; }

    public void OnPointerDown(PointerEventData e)
    {
        pointersDown++;
        LastPressTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData e) => pointersDown = Mathf.Max(0, pointersDown - 1);

    void OnDisable() => pointersDown = 0;   // hidden mid-press (pause, game over) → release
}
