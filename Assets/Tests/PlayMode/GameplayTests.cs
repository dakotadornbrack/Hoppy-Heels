using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Moment-to-moment gameplay: bouncing, wrapping, dying, the camera, scoring,
/// gems, moving platforms, pooling and settings.
/// </summary>
public class GameplayTests : HoppyHeelsTestBase
{
    // ── Player ───────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Player_BouncesOffFirstPlatform_AtJumpForce()
    {
        float jumpForce = Field<float>(Player.GetComponent<PlayerController>(), "jumpForce");
        GM.StartGame();

        float maxUp = 0f;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            maxUp = Mathf.Max(maxUp, PlayerBody.linearVelocity.y);
            yield return null;
        }
        Assert.AreEqual(jumpForce, maxUp, 0.5f, "Landing should launch the player at jumpForce");
        Assert.AreEqual(GameManager.GameState.Playing, GM.State, "Bouncing in place shouldn't kill the player");
    }

    [UnityTest]
    public IEnumerator Player_WrapsAroundScreenEdges()
    {
        GM.StartGame();
        yield return null;
        var cam = Camera.main;
        float half = cam.orthographicSize * cam.aspect;
        float y = Player.transform.position.y;

        yield return MovePlayerTo(new Vector2(half + 0.6f, y));
        yield return new WaitForFixedUpdate();
        Assert.Less(Player.transform.position.x, 0f, "Leaving the right edge should wrap to the left side");

        yield return MovePlayerTo(new Vector2(-half - 0.6f, Player.transform.position.y));
        yield return new WaitForFixedUpdate();
        Assert.Greater(Player.transform.position.x, 0f, "Leaving the left edge should wrap to the right side");
    }

    [UnityTest]
    public IEnumerator Player_FallingOffScreen_KillsOnlyAfterGracePeriod()
    {
        float grace = Field<float>(Cam, "graceTime");
        GM.StartGame();
        yield return new WaitForSeconds(0.2f);

        yield return MovePlayerTo(new Vector2(0f, Cam.BottomBoundaryY - 3f));
        yield return new WaitForSeconds(grace * 0.4f);
        Assert.AreEqual(GameManager.GameState.Playing, GM.State, "Should not die during the start-of-run grace period");

        yield return new WaitForSeconds(grace);
        Assert.AreEqual(GameManager.GameState.GameOver, GM.State, "Should die once below the camera after the grace period");
        Assert.IsTrue(UI("GameOverPanel").activeSelf);
    }

    [UnityTest]
    public IEnumerator Pause_FreezesTheWorld_ResumeContinues()
    {
        GM.StartGame();
        yield return new WaitForSeconds(0.3f);

        GM.PauseGame();
        Assert.AreEqual(0f, Time.timeScale);
        Vector3 frozenAt = Player.transform.position;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(frozenAt, Player.transform.position, "Player moved while paused");

        GM.ResumeGame();
        Assert.AreEqual(GameManager.GameState.Playing, GM.State);
        yield return new WaitForSeconds(0.2f);
        Assert.AreNotEqual(frozenAt, Player.transform.position, "Player should move again after resuming");
    }

    // ── Camera ───────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Camera_FollowsUpButNeverDown()
    {
        GM.StartGame();
        FreezePlayer();

        yield return MovePlayerTo(new Vector2(0f, 15f));
        yield return new WaitForSeconds(1.5f);
        float high = Camera.main.transform.position.y;
        Assert.Greater(high, 10f, "Camera should follow the player upward");

        yield return MovePlayerTo(new Vector2(0f, 12f));   // drop a little, still on screen
        yield return new WaitForSeconds(1f);
        Assert.GreaterOrEqual(Camera.main.transform.position.y, high - 0.001f, "Camera must never scroll down");
    }

    [UnityTest]
    public IEnumerator Camera_AutoScrollsAboveThreshold()
    {
        float startH = Field<float>(Cam, "autoScrollStartHeight");
        GM.StartGame();
        FreezePlayer();

        // Below the threshold, a stationary player means a stationary camera
        yield return MovePlayerTo(new Vector2(0f, startH - 10f));
        yield return new WaitForSeconds(1.5f);
        float y0 = Camera.main.transform.position.y;
        yield return new WaitForSeconds(1f);
        Assert.AreEqual(y0, Camera.main.transform.position.y, 0.05f, "Camera shouldn't creep below the threshold");

        // Above it, the camera keeps rising on its own
        yield return MovePlayerTo(new Vector2(0f, startH + 2f));
        yield return new WaitForSeconds(1.5f);
        float y1 = Camera.main.transform.position.y;
        yield return new WaitForSeconds(2f);
        Assert.Greater(Camera.main.transform.position.y - y1, 0.5f, "Camera should auto-scroll above the threshold");
    }

    // ── Scoring & gems ───────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Score_IsHeightTimesPointsPerUnit_AndNeverDrops()
    {
        var sm = ScoreManager.Instance;
        float ppu = Field<float>(sm, "pointsPerUnit");
        int gemValue = Field<int>(Pool("GemPool").GetComponentInChildren<Collectible>(true), "pointValue");
        float startY = Player.transform.position.y;

        // The teleported player can pass through a gem, so strip gem points out
        int HeightScore() => sm.CurrentScore - sm.GemsCollected * gemValue;

        GM.StartGame();
        FreezePlayer();
        yield return MovePlayerTo(new Vector2(0f, startY + 10.05f));
        Assert.AreEqual(Mathf.FloorToInt(10.05f * ppu), HeightScore());

        yield return MovePlayerTo(new Vector2(0f, startY + 5f));
        Assert.AreEqual(Mathf.FloorToInt(10.05f * ppu), HeightScore(), "Falling must not reduce the score");
    }

    [UnityTest]
    public IEnumerator GemBonus_IsKept_AfterClimbingPastIt()
    {
        var sm = ScoreManager.Instance;
        float ppu = Field<float>(sm, "pointsPerUnit");
        int gemValue = Field<int>(Pool("GemPool").GetComponentInChildren<Collectible>(true), "pointValue");
        float startY = Player.transform.position.y;

        GM.StartGame();
        FreezePlayer();
        yield return MovePlayerTo(new Vector2(0f, startY + 5.05f));
        sm.CollectGem(gemValue);                                       // bonus picked up low down...
        yield return MovePlayerTo(new Vector2(0f, startY + 20.05f));   // ...then climb well past it

        // (The teleport can also pass through spawned gems, so count every gem collected)
        int expected = Mathf.FloorToInt(20.05f * ppu) + sm.GemsCollected * gemValue;
        Assert.AreEqual(expected, sm.CurrentScore,
            "Gem bonus was absorbed by the height score instead of adding to it");
    }

    [UnityTest]
    public IEnumerator Gem_Pickup_AddsPointsAndCount_ThenReturnsToPool()
    {
        var sm = ScoreManager.Instance;
        GM.StartGame();
        yield return new WaitForSeconds(0.1f);

        var pool = Pool("GemPool");
        var gem = pool.Get(Player.transform.position);
        var collectible = gem.GetComponent<Collectible>();
        collectible.Init(pool);
        int value = Field<int>(collectible, "pointValue");
        int scoreBefore = sm.CurrentScore;

        for (int i = 0; i < 5 && gem.activeSelf; i++)
            yield return new WaitForFixedUpdate();

        Assert.IsFalse(gem.activeSelf, "Gem should be collected and returned to its pool");
        Assert.AreEqual(1, sm.GemsCollected);
        Assert.GreaterOrEqual(sm.CurrentScore, scoreBefore + value, "Gem should add its point value");
    }

    [UnityTest]
    public IEnumerator GameOver_SavesNewHighScore_AndAddsToTotalGems()
    {
        PlayerPrefs.SetInt("HoppyHeels_HighScore", 0);
        PlayerPrefs.SetInt("HoppyHeels_TotalGems", 7);
        yield return ReloadScene();   // pick up the values above

        var sm = ScoreManager.Instance;
        GM.StartGame();
        FreezePlayer();
        yield return MovePlayerTo(new Vector2(0f, Player.transform.position.y + 20f));
        sm.CollectGem(50);
        sm.CollectGem(50);
        int finalScore = sm.CurrentScore;

        GM.TriggerGameOver();
        Assert.AreEqual(finalScore, PlayerPrefs.GetInt("HoppyHeels_HighScore"));
        Assert.AreEqual(9, PlayerPrefs.GetInt("HoppyHeels_TotalGems"));
        Assert.AreEqual(finalScore, sm.HighScore);
    }

    [UnityTest]
    public IEnumerator GameOver_DoesNotLowerExistingHighScore()
    {
        PlayerPrefs.SetInt("HoppyHeels_HighScore", 999999);
        yield return ReloadScene();

        GM.StartGame();
        yield return new WaitForSeconds(0.3f);
        GM.TriggerGameOver();
        Assert.AreEqual(999999, PlayerPrefs.GetInt("HoppyHeels_HighScore"));
    }

    static IEnumerator ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName);
        yield return null;
        yield return null;
    }

    // ── Platforms & pooling ──────────────────────────────────────────

    [UnityTest]
    public IEnumerator MovingPlatform_OscillatesWithinRange()
    {
        GM.StartGame();
        var pool = Pool("MovingPool");
        var origin = new Vector3(0f, 80f, 0f);   // far above the player
        var mp = pool.Get(origin).GetComponent<MovingPlatform>();
        float range = Field<float>(mp, "range");
        float speed = Field<float>(mp, "speed");

        float minX = 0f, maxX = 0f;
        for (float t = 0f; t < 4f * range / speed + 0.5f; t += Time.deltaTime)
        {
            float dx = mp.transform.position.x - origin.x;
            minX = Mathf.Min(minX, dx);
            maxX = Mathf.Max(maxX, dx);
            Assert.AreEqual(origin.y, mp.transform.position.y, 0.001f, "Moving platform should only move horizontally");
            yield return null;
        }

        float slack = speed * 0.1f;   // one long frame of overshoot
        Assert.LessOrEqual(maxX, range + slack, "Moved too far right");
        Assert.GreaterOrEqual(minX, -range - slack, "Moved too far left");
        Assert.Greater(maxX, range * 0.8f, "Should reach the right end of its range");
        Assert.Less(minX, -range * 0.8f, "Should reach the left end of its range");
    }

    [UnityTest]
    public IEnumerator ObjectPool_ReusesReturnedObjects_AndGrowsWhenEmpty()
    {
        var pool = Pool("StaticPool");
        int baseCount = pool.transform.childCount;

        for (int i = 0; i < 25; i++)
            pool.Return(pool.Get(new Vector3(0f, 200f, 0f)));
        Assert.AreEqual(baseCount, pool.transform.childCount, "Get/Return cycles should reuse objects");

        var borrowed = new List<GameObject>();
        for (int i = 0; i < baseCount + 5; i++)
            borrowed.Add(pool.Get(new Vector3(i, 200f, 0f)));
        Assert.Greater(pool.transform.childCount, baseCount, "Pool should create new objects when it runs dry");
        Assert.AreEqual(borrowed.Count, new HashSet<GameObject>(borrowed).Count, "Pool handed out the same object twice");

        foreach (var go in borrowed) pool.Return(go);
        yield break;
    }

    // ── Settings ─────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Settings_SensitivityIsClampedAndSaved_SoundToggleMutes()
    {
        var settings = SettingsManager.Instance;

        settings.SetTiltSensitivity(1000f);
        Assert.AreEqual(SettingsManager.MaxSens, settings.TiltSensitivity);
        settings.SetTiltSensitivity(-5f);
        Assert.AreEqual(SettingsManager.MinSens, settings.TiltSensitivity);
        settings.SetTiltSensitivity(30f);
        Assert.AreEqual(30f, PlayerPrefs.GetFloat("HoppyHeels_TiltSensitivity"));

        settings.SetSoundOn(false);
        Assert.AreEqual(0f, AudioListener.volume);
        Assert.AreEqual(0, PlayerPrefs.GetInt("HoppyHeels_SoundOn"));
        settings.SetSoundOn(true);
        Assert.AreEqual(1f, AudioListener.volume);
        yield break;
    }
}
