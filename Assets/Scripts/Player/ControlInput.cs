using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Turns the chosen control mode into one steering value for PlayerController.
/// Tilt gives an analog value; touch-sides and on-screen buttons give -1 / 0 / +1.
/// Also shows the on-screen move buttons only in Buttons mode during a run.
/// Lives on the Player.
/// </summary>
public class ControlInput : MonoBehaviour
{
    [SerializeField] HoldButton leftButton;
    [SerializeField] HoldButton rightButton;

    /// <summary>A finger (or the mouse in the editor) touching the screen.</summary>
    public struct Pointer
    {
        public Vector2 position;   // screen pixels
        public float startTime;    // when it touched down; the newest touch wins
        public Pointer(Vector2 position, float startTime) { this.position = position; this.startTime = startTime; }
    }

    /// <summary>Tests can supply fake touches here instead of real ones.</summary>
    public static System.Func<IReadOnlyList<Pointer>> PointerOverride;

    public ControlMode Mode => SettingsManager.Instance != null ? SettingsManager.Instance.Controls : ControlMode.Tilt;

    /// <summary>True when Horizontal is -1 / 0 / +1 rather than an analog tilt.</summary>
    public bool IsDigital => Mode != ControlMode.Tilt;

    /// <summary>-1 = left, +1 = right. Analog for tilt, digital otherwise.</summary>
    public float Horizontal { get; private set; }

    readonly List<Pointer> pointers = new List<Pointer>();
    readonly Dictionary<int, float> touchStartTimes = new Dictionary<int, float>();
    readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    float mouseDownTime;
    bool buttonsShown = true;

    void Update()
    {
        bool playing = GameManager.Instance?.State == GameManager.GameState.Playing;
        ShowMoveButtons(playing && Mode == ControlMode.Buttons);

        Horizontal = Mode switch
        {
            ControlMode.TouchSides => FromTouchSides(),
            ControlMode.Buttons    => FromButtons(),
            _                      => FromTilt(),
        };

#if UNITY_EDITOR || UNITY_STANDALONE
        // Keyboard always works on desktop so every mode is testable in the editor
        float keys = Input.GetAxis("Horizontal");
        if (Mathf.Abs(keys) > 0.01f)
            Horizontal = IsDigital ? Mathf.Sign(keys) : keys;
#endif
    }

    // ── Modes ────────────────────────────────────────────────────────

    static float FromTilt()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return 0f;   // no accelerometer; the keyboard fallback above takes over
#else
        return Mathf.Clamp(Input.acceleration.x, -1f, 1f);
#endif
    }

    float FromButtons()
    {
        bool left  = leftButton  != null && leftButton.IsHeld;
        bool right = rightButton != null && rightButton.IsHeld;
        if (left && right) return rightButton.LastPressTime >= leftButton.LastPressTime ? 1f : -1f;
        return left ? -1f : right ? 1f : 0f;
    }

    float FromTouchSides()
    {
        var current = PointerOverride != null ? PointerOverride() : ReadPointers();
        return DirectionFromPointers(current, Screen.width, IsOverButton);
    }

    /// <summary>
    /// The newest touch that isn't on a button decides the direction:
    /// left half of the screen → -1, right half → +1, no touch → 0.
    /// </summary>
    public static float DirectionFromPointers(IReadOnlyList<Pointer> touches, float screenWidth,
                                              System.Func<Vector2, bool> isBlocked)
    {
        bool found = false;
        Pointer newest = default;
        foreach (var p in touches)
        {
            if (isBlocked != null && isBlocked(p.position)) continue;   // e.g. tapping Pause
            if (!found || p.startTime >= newest.startTime) { newest = p; found = true; }
        }
        if (!found) return 0f;
        return newest.position.x < screenWidth * 0.5f ? -1f : 1f;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    List<Pointer> ReadPointers()
    {
        pointers.Clear();

        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began || !touchStartTimes.ContainsKey(t.fingerId))
                touchStartTimes[t.fingerId] = Time.unscaledTime;

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                touchStartTimes.Remove(t.fingerId);
            else
                pointers.Add(new Pointer(t.position, touchStartTimes[t.fingerId]));
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // The mouse stands in for a finger on desktop
        if (Input.GetMouseButtonDown(0)) mouseDownTime = Time.unscaledTime;
        if (Input.GetMouseButton(0)) pointers.Add(new Pointer(Input.mousePosition, mouseDownTime));
#endif
        return pointers;
    }

    /// <summary>True if the screen point is over a button (Pause etc.), so touching it doesn't steer.</summary>
    bool IsOverButton(Vector2 screenPos)
    {
        var es = EventSystem.current;
        if (es == null) return false;

        raycastResults.Clear();
        es.RaycastAll(new PointerEventData(es) { position = screenPos }, raycastResults);
        foreach (var r in raycastResults)
            if (r.gameObject.GetComponentInParent<Selectable>() != null) return true;
        return false;
    }

    void ShowMoveButtons(bool show)
    {
        if (show == buttonsShown) return;
        buttonsShown = show;
        if (leftButton)  leftButton.gameObject.SetActive(show);
        if (rightButton) rightButton.gameObject.SetActive(show);
    }
}
