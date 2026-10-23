using System.Collections;
using UnityEngine;

/// <summary>
/// Shakes when the player lands on it, then fades out and drops away.
/// The spawner calls Init() to give it a reference to its own pool so it can self-return.
/// </summary>
public class BreakablePlatform : PlatformBase
{
    [SerializeField] float breakDelay = 0.2f;      // shake time before it gives way
    [SerializeField] float shakeAmount = 0.06f;    // max horizontal jitter (units)
    [SerializeField] float fadeDuration = 0.25f;
    [SerializeField] float fallDistance = 0.4f;    // how far it drops while fading

    ObjectPool sourcePool;
    SpriteRenderer[] renderers;
    Color[] baseColors;
    Collider2D col;
    Coroutine breakRoutine;

    /// <summary>Called by PlatformSpawner so the platform can return itself when done.</summary>
    public void Init(ObjectPool pool) => sourcePool = pool;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
    }

    public override void OnPlayerLanded()
    {
        if (breakRoutine != null) return;
        breakRoutine = StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        Vector3 basePos = transform.position;

        // Shake
        float t = 0f;
        while (t < breakDelay)
        {
            t += Time.deltaTime;
            float falloff = 1f - t / breakDelay * 0.5f;
            transform.position = basePos + Vector3.right * (Random.Range(-1f, 1f) * shakeAmount * falloff);
            yield return null;
        }
        transform.position = basePos;

        // Give way: no more collisions, fade and drop
        if (col) col.enabled = false;
        AudioManager.Instance?.Play(AudioManager.SFX.BreakPlatform);

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            transform.position = basePos + Vector3.down * (fallDistance * p * p);
            SetAlpha(1f - p);
            yield return null;
        }

        breakRoutine = null;
        Break();
    }

    void SetAlpha(float a)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = baseColors[i];
            c.a *= a;
            renderers[i].color = c;
        }
    }

    void Break()
    {
        if (sourcePool != null)
            sourcePool.Return(gameObject);
        else
            gameObject.SetActive(false);
    }

    public override void ResetPlatform()
    {
        StopAllCoroutines();
        breakRoutine = null;
        if (col) col.enabled = true;
        if (renderers != null) SetAlpha(1f);
    }

    void OnDisable()
    {
        // Coroutines stop when deactivated; clear the handle so it can break again after reuse
        breakRoutine = null;
    }
}
