using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Procedural generation: the level must always be climbable, get harder with
/// height, and recycle platforms instead of growing forever.
/// </summary>
public class PlatformSpawnerTests : HoppyHeelsTestBase
{
    PlatformSpawner Spawner => Object.FindFirstObjectByType<PlatformSpawner>();

    /// <summary>Highest a normal jump can carry the player: v² / 2g.</summary>
    float JumpApex()
    {
        float v = Field<float>(Player.GetComponent<PlayerController>(), "jumpForce");
        float g = Mathf.Abs(Physics2D.gravity.y) * PlayerBody.gravityScale;
        return v * v / (2f * g);
    }

    [UnityTest]
    public IEnumerator Level_StartsWithPlatformUnderfootAndPlatformsAhead()
    {
        var platforms = ActivePlatforms();
        int ahead = Field<int>(Spawner, "platformsAhead");

        Assert.GreaterOrEqual(platforms.Count, ahead, "Should pre-fill platforms above the player");

        Vector3 p = Player.transform.position;
        var first = platforms[0];
        Assert.That(first.transform.position.y, Is.LessThan(p.y).And.GreaterThan(p.y - 1f),
            "First platform should sit just below the player");
        Assert.AreEqual(0f, first.transform.position.x, 0.001f, "First platform should be centred");
        Assert.IsInstanceOf<PlatformBase>(first);
        Assert.IsNotInstanceOf<BreakablePlatform>(first, "First platform must be safe");
        yield break;
    }

    [UnityTest]
    public IEnumerator Climb_EveryGapIsReachable_AndDifficultyRamps()
    {
        var s       = Spawner;
        float minGap   = Field<float>(s, "minGapY");
        float easyMax  = Field<float>(s, "maxGapY");
        float hardMax  = Field<float>(s, "maxGapYHard");
        float rampH    = Field<float>(s, "difficultyRampHeight");
        float xRange   = Field<float>(s, "xRange");
        float apex     = JumpApex();
        const float eps = 0.001f;

        Assert.Less(hardMax, apex, "Tuning error: the hardest gap is higher than the player can jump");

        GM.StartGame();
        FreezePlayer();

        float largestLateGap = 0f;
        for (float y = 0f; y <= rampH * 1.5f; y += 4f)
        {
            yield return MovePlayerTo(new Vector2(0f, y));

            var ys = ActivePlatforms();
            for (int i = 1; i < ys.Count; i++)
            {
                var below = ys[i - 1].transform.position;
                var above = ys[i].transform.position;
                float gap = above.y - below.y;

                // Gap limit at this height (the spawner uses the player's height, which is ≤ the platform's)
                float allowed = Mathf.Lerp(easyMax, hardMax, Mathf.Clamp01(below.y / rampH));

                // Small tolerance: early on, the moving-platform cap (maxGapY × 0.55 = 0.99) dips just under minGapY
                Assert.GreaterOrEqual(gap, minGap - 0.02f, $"Platforms too close at y={below.y:F1}");
                Assert.LessOrEqual(gap, allowed + eps, $"Gap {gap:F2} at y={below.y:F1} exceeds difficulty limit {allowed:F2}");
                Assert.Less(gap, apex, $"Unreachable gap {gap:F2} at y={below.y:F1}");

                if (below.y > rampH) largestLateGap = Mathf.Max(largestLateGap, gap);
            }

            foreach (var p in ys.Where(p => !(p is MovingPlatform)))
                Assert.LessOrEqual(Mathf.Abs(p.transform.position.x), xRange + eps, "Platform spawned outside horizontal range");
        }

        Assert.Greater(largestLateGap, easyMax,
            "Above the ramp height, gaps should exceed the starting maximum (difficulty not ramping)");
    }

    [UnityTest]
    public IEnumerator Climb_RecyclesPlatformsBelowCamera_PoolsStayBounded()
    {
        var s = Spawner;
        float buffer = Field<float>(s, "despawnBuffer");
        string[] pools = { "StaticPool", "MovingPool", "BreakablePool", "GemPool" };
        int startObjects = pools.Sum(n => Pool(n).transform.childCount);

        GM.StartGame();
        FreezePlayer();
        for (float y = 0f; y <= 200f; y += 6f)
        {
            yield return MovePlayerTo(new Vector2(0f, y));
            yield return new WaitForSeconds(0.25f);   // let the camera catch up so recycling kicks in
        }
        yield return null;

        float despawnY = Cam.BottomBoundaryY - buffer;
        foreach (var p in ActivePlatforms())
            Assert.GreaterOrEqual(p.transform.position.y, despawnY - 0.01f, "Platform left alive below the despawn line");

        foreach (var gem in Pool("GemPool").GetComponentsInChildren<Collectible>(false))
            Assert.GreaterOrEqual(gem.transform.position.y, despawnY - 0.01f, "Gem left alive below the despawn line");

        int endObjects = pools.Sum(n => Pool(n).transform.childCount);
        Assert.LessOrEqual(endObjects, startObjects + 10,
            $"Pools grew from {startObjects} to {endObjects} objects over a 200-unit climb — objects aren't being reused");
    }

    [UnityTest]
    public IEnumerator Spawner_IsIdleOnMainMenu()
    {
        int before = ActivePlatforms().Count;
        Vector3 top = ActivePlatforms().Last().transform.position;

        FreezePlayer();
        yield return MovePlayerTo(new Vector2(0f, 50f));   // would normally trigger spawning
        yield return null;

        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);
        Assert.AreEqual(before, ActivePlatforms().Count, "Nothing should spawn before the run starts");
        Assert.AreEqual(top, ActivePlatforms().Last().transform.position);
    }
}
