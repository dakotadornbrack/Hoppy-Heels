using UnityEngine;

/// <summary>
/// A platform that kills the player on contact.
/// Tag this platform's GameObject "HazardPlatform" so PlayerController reacts correctly.
/// PlayerController checks for this tag before applying the bounce — it calls Die() instead.
/// </summary>
public class HazardPlatform : PlatformBase
{
    [SerializeField] Color hazardColor = new Color(0.9f, 0.2f, 0.2f);

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = hazardColor;
    }

    public override void OnPlayerLanded()
    {
        // PlayerController calls Die() before reaching this, but play the sound here
        // in case a future code path reaches it.
        AudioManager.Instance?.Play(AudioManager.SFX.Hazard);
    }
}
