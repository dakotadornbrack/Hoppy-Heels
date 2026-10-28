using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The three control modes: Tilt (default), touch-the-sides, and on-screen buttons,
/// plus the Settings selector that chooses between them.
/// </summary>
public class ControlModeTests : HoppyHeelsTestBase
{
    const string ModeKey = "HoppyHeels_ControlMode";

    static SettingsManager Settings => SettingsManager.Instance;
    static float MaxSpeed => Field<float>(Player.GetComponent<PlayerController>(), "maxSpeed");
    static HoldButton LeftButton  => UI("HUD/MoveLeftButton").GetComponent<HoldButton>();
    static HoldButton RightButton => UI("HUD/MoveRightButton").GetComponent<HoldButton>();

    static IEnumerator ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName);
        yield return null;
        yield return null;
    }

    static PointerEventData Press() => new PointerEventData(EventSystem.current);

    /// <summary>Waits for physics, then returns the player's horizontal speed.</summary>
    static IEnumerator WaitThenReadVx(float seconds, System.Action<float> result)
    {
        yield return new WaitForSeconds(seconds);
        yield return new WaitForFixedUpdate();
        result(PlayerBody.linearVelocity.x);
    }

    // ── Default & persistence ────────────────────────────────────────

    [UnityTest]
    public IEnumerator Default_IsTilt_WithSliderShown_AndNoMoveButtons()
    {
        PlayerPrefs.DeleteKey(ModeKey);
        yield return ReloadScene();

        Assert.AreEqual(ControlMode.Tilt, Settings.Controls);

        UI("MainMenuPanel/SettingsButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(UI("SettingsPanel/SensitivitySlider").activeSelf, "Tilt sensitivity should show in Tilt mode");
        UI("SettingsPanel/BackButton").GetComponent<Button>().onClick.Invoke();

        GM.StartGame();
        yield return null;
        Assert.IsFalse(UI("HUD/MoveLeftButton").activeSelf, "Move buttons should be hidden in Tilt mode");
        Assert.IsFalse(UI("HUD/MoveRightButton").activeSelf);
    }

    [UnityTest]
    public IEnumerator SettingsSelector_SwitchesMode_HighlightsIt_AndSaves()
    {
        var panel = UI("SettingsPanel").GetComponent<SettingsPanel>();
        Color selected   = Field<Color>(panel, "selectedColor");
        Color unselected = Field<Color>(panel, "unselectedColor");

        UI("MainMenuPanel/SettingsButton").GetComponent<Button>().onClick.Invoke();

        UI("SettingsPanel/TouchModeButton").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(ControlMode.TouchSides, Settings.Controls);
        Assert.AreEqual((int)ControlMode.TouchSides, PlayerPrefs.GetInt(ModeKey));
        Assert.AreEqual(selected,   UI("SettingsPanel/TouchModeButton").GetComponent<Image>().color);
        Assert.AreEqual(unselected, UI("SettingsPanel/TiltModeButton").GetComponent<Image>().color);
        Assert.IsFalse(UI("SettingsPanel/SensitivitySlider").activeSelf, "Tilt slider should hide for other modes");
        Assert.IsFalse(UI("SettingsPanel/SensLabel").activeSelf);

        UI("SettingsPanel/ButtonsModeButton").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(ControlMode.Buttons, Settings.Controls);

        // Survives a restart of the game
        yield return ReloadScene();
        Assert.AreEqual(ControlMode.Buttons, Settings.Controls);

        UI("MainMenuPanel/SettingsButton").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(selected, UI("SettingsPanel/ButtonsModeButton").GetComponent<Image>().color,
            "Reopening Settings should highlight the saved mode");

        UI("SettingsPanel/TiltModeButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(UI("SettingsPanel/SensitivitySlider").activeSelf, "Slider should come back for Tilt");
    }

    // ── On-screen buttons ────────────────────────────────────────────

    [UnityTest]
    public IEnumerator MoveButtons_OnlyShowDuringRunInButtonsMode()
    {
        Settings.SetControlMode(ControlMode.Buttons);
        yield return null;
        Assert.IsFalse(UI("HUD/MoveLeftButton").activeSelf, "Hidden on the main menu");

        GM.StartGame();
        yield return null;
        Assert.IsTrue(UI("HUD/MoveLeftButton").activeSelf, "Shown during a run");
        Assert.IsTrue(UI("HUD/MoveRightButton").activeSelf);

        GM.PauseGame();
        yield return null;
        Assert.IsFalse(UI("HUD/MoveLeftButton").activeSelf, "Hidden while paused");

        // Changing mode from the pause menu applies on resume
        Settings.SetControlMode(ControlMode.TouchSides);
        GM.ResumeGame();
        yield return null;
        Assert.IsFalse(UI("HUD/MoveLeftButton").activeSelf, "Hidden after switching to Touch");

        Settings.SetControlMode(ControlMode.Buttons);
        yield return null;
        Assert.IsTrue(UI("HUD/MoveRightButton").activeSelf);

        GM.TriggerGameOver();
        yield return null;
        Assert.IsFalse(UI("HUD/MoveRightButton").activeSelf, "Hidden on game over");
    }

    [UnityTest]
    public IEnumerator HoldingButtons_MovesAtMaxSpeed_ReleasingStops()
    {
        Settings.SetControlMode(ControlMode.Buttons);
        GM.StartGame();
        FreezePlayer();   // no gravity or landings, so friction can't skew the speed reading
        yield return null;
        float max = MaxSpeed;
        float vx = 0f;

        RightButton.OnPointerDown(Press());
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(max, vx, 0.01f, "Holding right should reach max speed");

        RightButton.OnPointerUp(Press());
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(0f, vx, 0.01f, "Letting go should stop");

        LeftButton.OnPointerDown(Press());
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(-max, vx, 0.01f, "Holding left should move left at max speed");

        // Both held: the newest press wins
        yield return new WaitForSecondsRealtime(0.05f);
        RightButton.OnPointerDown(Press());
        yield return WaitThenReadVx(0.3f, v => vx = v);   // full left → full right is twice the ramp
        Assert.AreEqual(max, vx, 0.01f, "Pressing right while holding left should switch to right");

        LeftButton.OnPointerUp(Press());
        RightButton.OnPointerUp(Press());
    }

    [UnityTest]
    public IEnumerator DigitalControls_RampUpQuickly_NotInstantly()
    {
        Settings.SetControlMode(ControlMode.Buttons);
        GM.StartGame();
        FreezePlayer();   // no gravity or landings, so friction can't skew the speed reading
        yield return null;
        yield return new WaitForFixedUpdate();

        RightButton.OnPointerDown(Press());
        yield return new WaitForFixedUpdate();
        float firstStep = PlayerBody.linearVelocity.x;
        Assert.Greater(firstStep, 0f);
        Assert.Less(firstStep, MaxSpeed, "Should ease in so quick taps allow small nudges");
        RightButton.OnPointerUp(Press());
    }

    // ── Touch the sides ──────────────────────────────────────────────

    [UnityTest]
    public IEnumerator TouchSides_LeftHalfGoesLeft_RightHalfGoesRight_NoTouchStops()
    {
        Settings.SetControlMode(ControlMode.TouchSides);
        GM.StartGame();
        FreezePlayer();   // no gravity or landings, so friction can't skew the speed reading
        float max = MaxSpeed;
        float vx = 0f;
        var touches = new List<ControlInput.Pointer>();
        ControlInput.PointerOverride = () => touches;

        touches.Add(new ControlInput.Pointer(new Vector2(Screen.width * 0.25f, Screen.height * 0.5f), 1f));
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(-max, vx, 0.01f, "Holding the left half should move left");

        touches.Clear();
        touches.Add(new ControlInput.Pointer(new Vector2(Screen.width * 0.75f, Screen.height * 0.5f), 2f));
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(max, vx, 0.01f, "Holding the right half should move right");

        touches.Clear();
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(0f, vx, 0.01f, "No touch should stop");
    }

    [UnityTest]
    public IEnumerator TouchSides_TappingPauseDoesNotSteer()
    {
        Settings.SetControlMode(ControlMode.TouchSides);
        GM.StartGame();
        FreezePlayer();   // no gravity or landings, so friction can't skew the speed reading
        yield return null;

        var pause = (RectTransform)UI("HUD/PauseButton").transform;
        Vector2 onPause = RectTransformUtility.WorldToScreenPoint(null, pause.position);
        Assert.Greater(onPause.x, Screen.width * 0.5f, "Test assumes Pause is on the right half");

        ControlInput.PointerOverride = () => new[] { new ControlInput.Pointer(onPause, 1f) };
        float vx = 1f;
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(0f, vx, 0.01f, "A finger on the Pause button must not also move the player right");
    }

    [Test]
    public void TouchSides_NewestTouchWins()
    {
        var older = new ControlInput.Pointer(new Vector2(100f, 500f), 1f);   // left
        var newer = new ControlInput.Pointer(new Vector2(900f, 500f), 2f);   // right
        Assert.AreEqual( 1f, ControlInput.DirectionFromPointers(new[] { older, newer }, 1000f, null));
        Assert.AreEqual(-1f, ControlInput.DirectionFromPointers(new[] { newer, new ControlInput.Pointer(new Vector2(100f, 500f), 3f) }, 1000f, null));
        Assert.AreEqual( 0f, ControlInput.DirectionFromPointers(new ControlInput.Pointer[0], 1000f, null));
        Assert.AreEqual(-1f, ControlInput.DirectionFromPointers(new[] { older, newer }, 1000f, p => p.x > 500f),
            "A blocked touch (on a button) is skipped even if it's newest");
    }

    [UnityTest]
    public IEnumerator TiltMode_IgnoresScreenTouches()
    {
        Settings.SetControlMode(ControlMode.Tilt);
        GM.StartGame();
        FreezePlayer();   // no gravity or landings, so friction can't skew the speed reading
        ControlInput.PointerOverride = () => new[] { new ControlInput.Pointer(new Vector2(Screen.width * 0.1f, Screen.height * 0.5f), 1f) };

        float vx = 1f;
        yield return WaitThenReadVx(0.2f, v => vx = v);
        Assert.AreEqual(0f, vx, 0.01f, "Touches shouldn't steer in Tilt mode");
    }
}
