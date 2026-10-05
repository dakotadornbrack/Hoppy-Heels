using UnityEngine;

/// <summary>
/// Moves horizontally back and forth between ±range units from its spawn position.
/// </summary>
public class MovingPlatform : PlatformBase
{
    [SerializeField] float speed = 2f;
    [SerializeField] float range = 2.5f;

    Vector3 origin;
    float dir;

    void OnEnable()
    {
        origin = transform.position;
        dir = Random.value > 0.5f ? 1f : -1f;
    }

    void Update()
    {
        if (GameManager.Instance?.State != GameManager.GameState.Playing) return;

        float newX = transform.position.x + dir * speed * Time.deltaTime;

        if (Mathf.Abs(newX - origin.x) >= range)
            dir *= -1f;

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
    }

    public override void ResetPlatform()
    {
        origin = transform.position;
        dir = Random.value > 0.5f ? 1f : -1f;
    }
}
