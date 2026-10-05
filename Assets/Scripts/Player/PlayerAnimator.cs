using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural squash/stretch animation driven by Rigidbody2D velocity.
/// No extra sprites needed — works with any single sprite.
/// Attach to the same GameObject as PlayerController.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Squash on Land")]
    [SerializeField] Vector2 squashScale = new Vector2(1.3f, 0.7f);
    [SerializeField] float squashDuration = 0.15f;

    [Header("Airborne Stretch")]
    [SerializeField] Vector2 riseScale = new Vector2(0.85f, 1.15f);
    [SerializeField] Vector2 fallScale = new Vector2(0.95f, 1.05f);
    [SerializeField] float stretchSmoothing = 8f;

    [Header("Rise Tilt")]
    [SerializeField] float maxTiltAngle = 20f;
    [SerializeField] float tiltSmoothing = 10f;

    [Header("Visual Target")]
    [SerializeField] Transform visual;

    Rigidbody2D rb;
    Coroutine squashRoutine;
    bool dead;
    float currentTilt;

    static readonly Vector3 One = Vector3.one;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
    }

    void LateUpdate()
    {
        if (dead || rb == null || visual == null) return;

        // While a squash coroutine is running, let it drive the scale
        if (squashRoutine != null) return;

        Vector3 target;
        float vy = rb.linearVelocity.y;

        if (vy > 1f)
        {
            // Rising — stretch tall & narrow
            float t = Mathf.Clamp01(vy / 20f);
            target = Vector3.Lerp(One, new Vector3(riseScale.x, riseScale.y, 1f), t);
        }
        else if (vy < -1f)
        {
            // Falling — subtle stretch
            float t = Mathf.Clamp01(-vy / 20f);
            target = Vector3.Lerp(One, new Vector3(fallScale.x, fallScale.y, 1f), t);
        }
        else
        {
            target = One;
        }

        visual.localScale = Vector3.Lerp(visual.localScale, target,
            Time.unscaledDeltaTime * stretchSmoothing);

        // Tilt left while rising, return to upright while falling/landing
        float targetTilt = vy > 1f ? -maxTiltAngle * Mathf.Clamp01(vy / 10f) : 0f;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.unscaledDeltaTime * tiltSmoothing);
        visual.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    /// <summary>Called by PlayerController when the player bounces off a platform.</summary>
    public void OnLand()
    {
        if (dead) return;
        if (squashRoutine != null)
            StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(SquashCoroutine());
    }

    /// <summary>Called by PlayerController on death.</summary>
    public void OnDeath()
    {
        dead = true;
        if (squashRoutine != null)
            StopCoroutine(squashRoutine);
        squashRoutine = null;
        currentTilt = 0f;
        if (visual != null)
            visual.localRotation = Quaternion.identity;
        StartCoroutine(DeathCoroutine());
    }

    IEnumerator SquashCoroutine()
    {
        if (visual == null)
        {
            squashRoutine = null;
            yield break;
        }

        // Quick squash down
        Vector3 squash = new Vector3(squashScale.x, squashScale.y, 1f);
        float half = squashDuration * 0.35f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            visual.localScale = Vector3.Lerp(One, squash, t);
            yield return null;
        }

        // Spring back to normal
        elapsed = 0f;
        float recover = squashDuration * 0.65f;
        while (elapsed < recover)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recover;
            // Slight overshoot for springy feel
            float eased = 1f + (1f - t) * Mathf.Sin(t * Mathf.PI);
            visual.localScale = Vector3.Lerp(squash, One, eased);
            yield return null;
        }

        visual.localScale = One;
        squashRoutine = null;
    }

    IEnumerator DeathCoroutine()
    {
        if (visual == null) yield break;

        // Shrink and fade over 0.4s
        Vector3 startScale = visual.localScale;
        SpriteRenderer[] srs = visual.GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) startColors[i] = srs[i].color;

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled since timeScale may be 0
            float t = elapsed / duration;
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad
            visual.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
            for (int i = 0; i < srs.Length; i++)
            {
                Color c = startColors[i];
                srs[i].color = Color.Lerp(c, new Color(c.r, c.g, c.b, 0f), ease);
            }
            yield return null;
        }
    }
}
