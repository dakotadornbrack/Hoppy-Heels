using UnityEngine;

/// <summary>
/// Disappears a short time after the player lands on it.
/// The spawner calls Init() to give it a reference to its own pool so it can self-return.
/// </summary>
public class BreakablePlatform : PlatformBase
{
    [SerializeField] float breakDelay = 0.2f;

    ObjectPool sourcePool;

    /// <summary>Called by PlatformSpawner so the platform can return itself when done.</summary>
    public void Init(ObjectPool pool) => sourcePool = pool;

    public override void OnPlayerLanded()
    {
        // Shake or flash could go here
        Invoke(nameof(Break), breakDelay);
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
        CancelInvoke();
    }

    void OnDisable()
    {
        CancelInvoke();
    }
}
