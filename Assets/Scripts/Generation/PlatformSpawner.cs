using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurally spawns platforms above the player and recycles them as they fall
/// below the camera. Difficulty scales the vertical gap as the player climbs.
/// </summary>
public class PlatformSpawner : MonoBehaviour
{
    [Header("Pools — assign one ObjectPool per platform type")]
    [SerializeField] ObjectPool staticPool;
    [SerializeField] ObjectPool movingPool;
    [SerializeField] ObjectPool breakablePool;

    [Header("Spawn Layout")]
    [SerializeField] float minGapY = 1.0f;      // min vertical space between platforms
    [SerializeField] float maxGapY = 1.8f;      // max vertical space at start
    [SerializeField] float maxGapYHard = 2.6f;  // max vertical space at peak difficulty
    [SerializeField] float xRange = 3.5f;        // horizontal spawn range
    [SerializeField] int platformsAhead = 14;    // how many platforms to keep alive above the player
    [SerializeField] float despawnBuffer = 6f;   // extra units below camera before recycling

    [Header("Difficulty")]
    [SerializeField] float difficultyRampHeight = 100f; // height at which max difficulty is reached

    [Header("Platform Type Weights (0–1)")]
    [SerializeField] float movingChance = 0.20f;
    [SerializeField] float breakableChance = 0.10f;

    [Header("Gem Spawning")]
    [SerializeField] ObjectPool gemPool;
    [SerializeField] float gemSpawnChance = 0.30f;
    [SerializeField] float gemFloatMinOffset = 0.5f;
    [SerializeField] float gemFloatMaxOffset = 1.5f;

    Transform player;
    CameraFollow camFollow;

    float nextSpawnY;

    // Tracks every live platform and which pool owns it
    readonly List<(GameObject go, ObjectPool pool)> active =
        new List<(GameObject, ObjectPool)>();

    // Tracks every live gem
    readonly List<(GameObject go, ObjectPool pool)> activeGems =
        new List<(GameObject, ObjectPool)>();

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        camFollow = Camera.main.GetComponent<CameraFollow>();

        if (player == null)
        {
            Debug.LogError("PlatformSpawner: No GameObject tagged 'Player' found.");
            return;
        }

        // Guaranteed first platform directly underfoot
        nextSpawnY = player.position.y - 0.3f;
        SpawnAt(new Vector3(0f, nextSpawnY, 0f), staticPool);
        nextSpawnY += Random.Range(minGapY, maxGapY);

        // Pre-fill the space above the player
        for (int i = 0; i < platformsAhead; i++)
            SpawnNext();
    }

    void Update()
    {
        if (player == null ||
            GameManager.Instance?.State != GameManager.GameState.Playing) return;

        // Keep platformsAhead platforms above the player at all times
        float lookahead = Camera.main.orthographicSize * 3f;
        while (active.Count < platformsAhead || nextSpawnY < player.position.y + lookahead)
            SpawnNext();

        // Recycle platforms that have scrolled off the bottom
        float despawnY = (camFollow != null ? camFollow.BottomBoundaryY : player.position.y - 15f)
                         - despawnBuffer;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var (go, pool) = active[i];

            // Clean up if already returned (e.g. breakable self-returned)
            if (go == null || !go.activeInHierarchy)
            {
                active.RemoveAt(i);
                continue;
            }

            if (go.transform.position.y < despawnY)
            {
                pool.Return(go);
                active.RemoveAt(i);
            }
        }

        RecycleGems(despawnY);
    }

    void SpawnNext()
    {
        float t = player != null
            ? Mathf.Clamp01(player.position.y / difficultyRampHeight)
            : 0f;
        float gapMax = Mathf.Lerp(maxGapY, maxGapYHard, t);

        // Pick platform type first so we can adjust the gap
        ObjectPool pool = PickPool();

        bool isMoving = (pool == movingPool);

        // Moving platforms get a hard-capped gap — the player also has to
        // time the horizontal oscillation
        float gap = isMoving
            ? Random.Range(minGapY, Mathf.Min(gapMax * 0.55f, 1.5f))
            : Random.Range(minGapY, gapMax);

        // Shrink horizontal range when vertical gap is large so the
        // combined diagonal distance stays reachable
        float gapRatio = Mathf.InverseLerp(minGapY, maxGapYHard, gap);
        float xAllowed = isMoving
            ? xRange * 0.4f                                       // moving platforms stay near center
            : Mathf.Lerp(xRange, xRange * 0.45f, gapRatio);
        float x = Random.Range(-xAllowed, xAllowed);

        Vector3 pos = new Vector3(x, nextSpawnY, 0f);
        SpawnAt(pos, pool);

        nextSpawnY += gap;

        // Occasionally spawn a gem near this platform
        if (gemPool != null && Random.value < gemSpawnChance)
        {
            // Keep gems away from screen edges
            float halfScreen = Camera.main.orthographicSize * Camera.main.aspect;
            float gemEdgePadding = 0.6f;
            float gemXMax = halfScreen - gemEdgePadding;

            Vector3 gemPos;
            if (Random.value < 0.5f)
            {
                // On top of platform, clamped to screen bounds
                float gx = Mathf.Clamp(pos.x, -gemXMax, gemXMax);
                gemPos = new Vector3(gx, pos.y + 0.5f, 0f);
            }
            else
            {
                // Floating between platforms
                float gemX = Random.Range(-gemXMax, gemXMax);
                float gemY = pos.y + Random.Range(gemFloatMinOffset, gemFloatMaxOffset);
                gemPos = new Vector3(gemX, gemY, 0f);
            }
            SpawnGem(gemPos);
        }
    }

    void SpawnAt(Vector3 pos, ObjectPool pool)
    {
        if (pool == null) pool = staticPool;
        var go = pool.Get(pos);
        go.GetComponent<BreakablePlatform>()?.Init(pool);
        active.Add((go, pool));
    }

    ObjectPool PickPool()
    {
        float roll = Random.value;

        if (breakablePool != null && roll < breakableChance)
            return breakablePool;

        if (movingPool != null && roll < breakableChance + movingChance)
            return movingPool;

        return staticPool;
    }

    void SpawnGem(Vector3 pos)
    {
        var go = gemPool.Get(pos);
        go.GetComponent<Collectible>()?.Init(gemPool);
        activeGems.Add((go, gemPool));
    }

    void RecycleGems(float despawnY)
    {
        for (int i = activeGems.Count - 1; i >= 0; i--)
        {
            var (go, pool) = activeGems[i];

            // Already collected (returned to pool)
            if (go == null || !go.activeInHierarchy)
            {
                activeGems.RemoveAt(i);
                continue;
            }

            if (go.transform.position.y < despawnY)
            {
                pool.Return(go);
                activeGems.RemoveAt(i);
            }
        }
    }
}
