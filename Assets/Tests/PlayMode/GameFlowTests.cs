using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Play-mode tests that load the real game scene and drive it through the
/// menu → run → game over → restart/menu loop, plus platform behaviour.
/// </summary>
public class GameFlowTests : HoppyHeelsTestBase
{
    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Clicks a button and waits until the scene has reloaded.</summary>
    static IEnumerator ClickAndWaitForReload(string buttonPath)
    {
        var oldGM = GM;
        UI(buttonPath).GetComponent<Button>().onClick.Invoke();

        for (int i = 0; i < 120 && (GM == null || GM == oldGM); i++)
            yield return null;
        Assert.AreNotSame(oldGM, GM, "Scene did not reload");
        yield return null;
    }

    static IEnumerator StartRunThenDie()
    {
        GM.StartGame();
        yield return new WaitForSeconds(0.2f);
        GM.TriggerGameOver();
        yield return null;
        Assert.IsTrue(UI("GameOverPanel").activeSelf, "Game-over panel should be showing");
    }

    // ── Menu ─────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Scene_OpensOnMainMenu_WithGradientBackground()
    {
        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);
        Assert.AreEqual(0f, Time.timeScale);

        var menu = UI("MainMenuPanel");
        Assert.IsTrue(menu.activeSelf);

        var bg = menu.GetComponent<Image>();
        Assert.IsNotNull(bg.sprite, "Main menu should use the gradient sprite, not a flat colour");
        Assert.AreEqual(Color.white, bg.color);
        yield break;
    }

    // ── Game over buttons ────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TryAgain_StartsNewRunImmediately()
    {
        yield return StartRunThenDie();
        yield return ClickAndWaitForReload("GameOverPanel/RestartButton");

        Assert.AreEqual(GameManager.GameState.Playing, GM.State);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(UI("MainMenuPanel").activeSelf, "Main menu should be skipped");
        Assert.IsTrue(UI("HUD/PauseButton").activeSelf, "Pause button should be visible");

        // Score tracking must be live: the player bounces off the first platform
        yield return new WaitForSeconds(1f);
        Assert.Greater(ScoreManager.Instance.CurrentScore, 0, "Score should be counting in the restarted run");
    }

    [UnityTest]
    public IEnumerator MainMenuButton_ReturnsToMenu()
    {
        yield return StartRunThenDie();
        yield return ClickAndWaitForReload("GameOverPanel/MainMenuButton");

        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);
        Assert.IsTrue(UI("MainMenuPanel").activeSelf);
        Assert.IsFalse(UI("GameOverPanel").activeSelf);
    }

    [UnityTest]
    public IEnumerator MainMenu_AfterTryAgain_StillStopsOnMenu()
    {
        yield return StartRunThenDie();
        yield return ClickAndWaitForReload("GameOverPanel/RestartButton");
        yield return DieDuringRun();
        yield return ClickAndWaitForReload("GameOverPanel/MainMenuButton");

        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);
    }

    static IEnumerator DieDuringRun()
    {
        yield return new WaitForSeconds(0.2f);
        GM.TriggerGameOver();
        yield return null;
    }

    [UnityTest]
    public IEnumerator PauseRestart_StartsNewRun()
    {
        GM.StartGame();
        yield return null;
        UI("HUD/PauseButton").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(GameManager.GameState.Paused, GM.State);

        yield return ClickAndWaitForReload("PausePanel/RestartButton");
        Assert.AreEqual(GameManager.GameState.Playing, GM.State);
    }

    // ── Settings ─────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SettingsSlider_HasLabelThatDoesNotOverlapIt()
    {
        UI("MainMenuPanel/SettingsButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue(UI("SettingsPanel").activeSelf);

        var bg = UI("SettingsPanel").GetComponent<Image>();
        Assert.IsNotNull(bg.sprite, "Settings should use the gradient background");
        Assert.AreEqual(1f, bg.color.a, "Settings background must be opaque so the menu doesn't show through");

        var label = UI("SettingsPanel/SensLabel").GetComponent<TMP_Text>();
        Assert.AreEqual("Tilt Sensitivity", label.text);

        Rect labelRect  = WorldRect((RectTransform)label.transform);
        Rect sliderRect = WorldRect((RectTransform)UI("SettingsPanel/SensitivitySlider").transform);
        Assert.IsFalse(labelRect.Overlaps(sliderRect), "Label overlaps the slider");
    }

    static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    // ── Breakable platform ───────────────────────────────────────────

    [UnityTest]
    public IEnumerator BreakablePlatform_ShakesFadesAndReturnsToPool()
    {
        GM.StartGame();
        var pool = GameObject.Find("BreakablePool").GetComponent<ObjectPool>();

        var basePos = new Vector3(50f, 50f, 0f);   // well away from the player
        var go  = pool.Get(basePos);
        var bp  = go.GetComponent<BreakablePlatform>();
        var col = go.GetComponent<Collider2D>();
        var srs = go.GetComponentsInChildren<SpriteRenderer>();
        bp.Init(pool);

        bp.OnPlayerLanded();

        // Shake phase: jitters horizontally, still solid
        float maxJitter = 0f;
        float t = 0f;
        while (t < 0.15f)
        {
            yield return null;
            t += Time.deltaTime;
            maxJitter = Mathf.Max(maxJitter, Mathf.Abs(go.transform.position.x - basePos.x));
        }
        Assert.Greater(maxJitter, 0f, "Platform should shake");
        Assert.Less(maxJitter, 0.2f, "Shake should be subtle");
        Assert.IsTrue(col.enabled, "Still solid while shaking");

        // Fade phase: collider off, fading, dropping
        yield return new WaitForSeconds(0.15f);
        Assert.IsFalse(col.enabled, "Player should fall through once it gives way");
        Assert.Less(srs[0].color.a, 1f, "Should be fading");
        Assert.Less(go.transform.position.y, basePos.y, "Should drop while fading");

        // Gone and back in the pool
        yield return new WaitForSeconds(0.4f);
        Assert.IsFalse(go.activeSelf, "Should return itself to the pool");

        // Reused from the pool: fully restored
        bp.ResetPlatform();
        go.SetActive(true);
        Assert.IsTrue(col.enabled);
        foreach (var sr in srs) Assert.AreEqual(1f, sr.color.a, 0.001f);
    }
}
