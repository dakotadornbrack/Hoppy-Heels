using System.Collections;
using UnityEngine;

/// <summary>
/// Attach alongside PlatformBase on any platform tagged "BouncePlatform".
/// Plays a scale-pulse animation and the boost sound when the player lands.
/// No extra tag logic needed here — PlayerController reads the tag directly.
/// </summary>
public class BouncePlatform : PlatformBase
{
    [SerializeField] float punchScale = 1.3f;
    [SerializeField] float punchDuration = 0.12f;

    Vector3 originalScale;

    void Awake() => originalScale = transform.localScale;

    public override void OnPlayerLanded()
    {
        AudioManager.Instance?.Play(AudioManager.SFX.BounceBoost);
        StopAllCoroutines();
        StartCoroutine(PunchRoutine());
    }

    public override void ResetPlatform()
    {
        StopAllCoroutines();
        transform.localScale = originalScale;
    }

    IEnumerator PunchRoutine()
    {
        float half = punchDuration * 0.5f;
        float t = 0f;

        // Scale up
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, punchScale, t / half);
            transform.localScale = originalScale * s;
            yield return null;
        }

        t = 0f;

        // Scale back down
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(punchScale, 1f, t / half);
            transform.localScale = originalScale * s;
            yield return null;
        }

        transform.localScale = originalScale;
    }
}
