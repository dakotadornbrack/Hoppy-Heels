using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Mobile behaviour (backgrounding, Android back button), the gem sparkle
/// material fix, and the neon menu styling.
/// </summary>
public class MobileAndPolishTests : HoppyHeelsTestBase
{
    static BackButtonHandler Back
    {
        get
        {
            var b = GM.GetComponent<BackButtonHandler>();
            Assert.IsNotNull(b, "BackButtonHandler missing from the GameManager object");
            return b;
        }
    }

    // ── Backgrounding ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator AppBackgrounded_DuringRun_Pauses()
    {
        GM.StartGame();
        yield return new WaitForSeconds(0.2f);

        GM.SendMessage("OnApplicationPause", true);   // what Android sends on home / app switch / call
        Assert.AreEqual(GameManager.GameState.Paused, GM.State);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(UI("PausePanel").activeSelf, "Pause menu should be waiting when the player returns");

        GM.SendMessage("OnApplicationPause", false);  // returning must NOT auto-resume
        Assert.AreEqual(GameManager.GameState.Paused, GM.State);
    }

    [UnityTest]
    public IEnumerator AppBackgrounded_OnMenuOrGameOver_ChangesNothing()
    {
        GM.SendMessage("OnApplicationPause", true);
        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);

        GM.StartGame();
        yield return new WaitForSeconds(0.2f);
        GM.TriggerGameOver();
        GM.SendMessage("OnApplicationPause", true);
        Assert.AreEqual(GameManager.GameState.GameOver, GM.State);
        Assert.IsFalse(UI("PausePanel").activeSelf);
    }

    // ── Back button ──────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Back_DuringRun_PausesThenResumes()
    {
        GM.StartGame();
        yield return null;

        Back.HandleBack();
        Assert.AreEqual(GameManager.GameState.Paused, GM.State);
        Assert.IsTrue(UI("PausePanel").activeSelf);

        Back.HandleBack();
        Assert.AreEqual(GameManager.GameState.Playing, GM.State);
        Assert.IsFalse(UI("PausePanel").activeSelf);
    }

    [UnityTest]
    public IEnumerator Back_ClosesSettingsFirst_FromMenuAndPause()
    {
        // From the main menu
        UI("MainMenuPanel/SettingsButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(UI("SettingsPanel").activeSelf);
        Back.HandleBack();
        Assert.IsFalse(UI("SettingsPanel").activeSelf, "Back should close Settings");
        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State, "...and not quit or start anything");

        // From the pause menu
        GM.StartGame();
        yield return null;
        GM.PauseGame();
        UI("PausePanel/SettingsButton").GetComponent<Button>().onClick.Invoke();
        Back.HandleBack();
        Assert.IsFalse(UI("SettingsPanel").activeSelf);
        Assert.AreEqual(GameManager.GameState.Paused, GM.State, "Closing Settings should leave the game paused");
    }

    [UnityTest]
    public IEnumerator Back_OnGameOver_GoesToMainMenu()
    {
        GM.StartGame();
        yield return new WaitForSeconds(0.2f);
        GM.TriggerGameOver();

        var oldGM = GM;
        Back.HandleBack();
        for (int i = 0; i < 120 && (GM == null || GM == oldGM); i++)
            yield return null;
        yield return null;

        Assert.AreEqual(GameManager.GameState.MainMenu, GM.State);
        Assert.IsTrue(UI("MainMenuPanel").activeSelf);
    }

    // ── Gem sparkle ──────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator GemSparkles_ShareOneMaterial()
    {
        var a = new GameObject("SparkleA").AddComponent<GemSparkle>();
        var b = new GameObject("SparkleB").AddComponent<GemSparkle>();
        yield return null;   // Start() builds the particle systems

        var ma = a.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        var mb = b.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        Assert.IsNotNull(ma);
        Assert.AreSame(ma, mb, "Each sparkle created its own material (memory leak)");
        Assert.AreSame(GemSparkle.SharedMaterial, ma);
    }

    // ── Neon menu style ──────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Menus_UseRoundedNeonButtons_AndMonotonTitles()
    {
        var canvas = GameObject.Find("UI Canvas");
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
        {
            var img = (Image)button.targetGraphic;
            Assert.IsNotNull(img.sprite, $"{button.name} has no rounded sprite");
            Assert.AreEqual(Image.Type.Sliced, img.type, $"{button.name} should be 9-sliced");
            Assert.IsNotNull(button.GetComponent<Outline>(), $"{button.name} is missing its neon rim");
        }

        foreach (var panel in new[] { "MainMenuPanel", "GameOverPanel", "PausePanel", "SettingsPanel" })
        {
            var title = UI(panel + "/TitleText").GetComponent<TMP_Text>();
            StringAssert.Contains("Monoton", title.font.name, $"{panel} title should use the Monoton font");
            Assert.IsTrue(title.fontSharedMaterial.IsKeywordEnabled("GLOW_ON"), $"{panel} title should glow");
        }
        yield break;
    }
}
