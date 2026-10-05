using UnityEngine;

/// <summary>
/// Base class for all platform types.
/// Override OnPlayerLanded() to add behaviour when the cup bounces off.
/// Override ResetPlatform() to restore state when the object is recycled from the pool.
/// </summary>
public class PlatformBase : MonoBehaviour
{
    public virtual void OnPlayerLanded() { }
    public virtual void ResetPlatform() { }
}
