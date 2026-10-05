using UnityEngine;

/// <summary>
/// Hybrid camera: follows the player upward (never down) early on, then adds
/// auto-scroll pressure after a height threshold.  Exposes the camera's bottom
/// edge for death detection.  Attach to the Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] Transform target;
    [SerializeField] float yOffset = 2f;            // headroom above player (lower = player higher on screen)
    [SerializeField] float smoothSpeed = 5f;

    [Header("Auto-Scroll (kicks in after a height threshold)")]
    [SerializeField] float autoScrollStartHeight = 30f;  // player world-Y that triggers auto-scroll
    [SerializeField] float autoScrollBaseSpeed   = 0.4f;  // initial scroll speed (units/sec)
    [SerializeField] float autoScrollAccel       = 0.01f; // extra speed gained per second

    [Header("Grace Period")]
    [SerializeField] float graceTime = 2f;          // seconds at start before death-by-camera is active

    Camera cam;
    float highestTargetY;

    // Auto-scroll state
    float autoScrollY;
    float autoScrollTimer;
    bool  autoScrollActive;

    float gameStartTime;

    /// <summary>World Y position of the bottom edge of the camera view.</summary>
    public float BottomBoundaryY => transform.position.y - cam.orthographicSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null) return;
        highestTargetY = target.position.y + yOffset;
        transform.position = new Vector3(transform.position.x, highestTargetY, transform.position.z);
        gameStartTime = Time.time;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ── Player-follow target (only advances upward) ──────────────
        float followY = target.position.y + yOffset;
        if (followY > highestTargetY)
            highestTargetY = followY;

        // ── Auto-scroll target ───────────────────────────────────────
        if (!autoScrollActive && target.position.y >= autoScrollStartHeight)
        {
            autoScrollActive = true;
            autoScrollY = highestTargetY;   // start from current camera height
            autoScrollTimer = 0f;
        }

        if (autoScrollActive)
        {
            autoScrollTimer += Time.deltaTime;
            float speed = autoScrollBaseSpeed + autoScrollAccel * autoScrollTimer;
            autoScrollY += speed * Time.deltaTime;
        }

        // ── Pick whichever is higher ─────────────────────────────────
        float desiredY = autoScrollActive
            ? Mathf.Max(highestTargetY, autoScrollY)
            : highestTargetY;

        float newY = Mathf.Lerp(transform.position.y, desiredY, smoothSpeed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // ── Death check (with early-game grace) ──────────────────────
        bool graceOver = Time.time - gameStartTime > graceTime;
        if (graceOver &&
            GameManager.Instance?.State == GameManager.GameState.Playing &&
            target.position.y < BottomBoundaryY - 0.5f)
        {
            target.GetComponent<PlayerController>()?.Die();
        }
    }
}
