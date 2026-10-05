using UnityEngine;

/// <summary>
/// Attach to a gem prefab. Uses a trigger collider — when the player enters,
/// the gem adds to the score, plays a sound, and returns itself to the pool.
/// </summary>
public class Collectible : MonoBehaviour
{
    [SerializeField] int pointValue = 50;

    ObjectPool sourcePool;

    /// <summary>Called by PlatformSpawner after Get() so the gem can self-return.</summary>
    public void Init(ObjectPool pool) => sourcePool = pool;

    void OnEnable()
    {
        // Ensure the sprite is visible when recycled from the pool
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        ScoreManager.Instance?.CollectGem(pointValue);
        AudioManager.Instance?.Play(AudioManager.SFX.CollectBean);

        // Sparkle burst at gem position
        var fx = new GameObject("GemSparkle");
        fx.transform.position = transform.position;
        fx.AddComponent<GemSparkle>();

        if (sourcePool != null)
            sourcePool.Return(gameObject);
        else
            gameObject.SetActive(false);
    }
}
