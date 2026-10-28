using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Adds the control-mode UI: Tilt / Touch / Buttons selector in Settings and the
/// on-screen move buttons in the HUD's bottom corners, and wires ControlInput on
/// the Player. Idempotent — reuses existing objects and replaces button wiring.
/// Used by SceneBuilder (new scenes) and SceneUpdater (existing scene).
/// </summary>
public static class ControlsSetup
{
    // Chevrons are in the default LiberationSans atlas; ◀ ▶ are not
    const string LeftArrow  = "‹";   // ‹
    const string RightArrow = "›";   // ›

    public static void Apply(GameObject canvas, GameObject player)
    {
        var settingsGO = canvas.transform.Find("SettingsPanel")?.gameObject;
        var hud        = canvas.transform.Find("HUD")?.gameObject;
        if (settingsGO == null || hud == null || player == null)
        {
            Debug.LogError("[ControlsSetup] SettingsPanel, HUD or Player not found");
            return;
        }

        SetupSettingsPanel(settingsGO);
        var (left, right) = SetupMoveButtons(hud);

        if (!player.TryGetComponent(out ControlInput input))
            input = Undo.AddComponent<ControlInput>(player);
        SceneBuilder.SetRef(input, "leftButton", left);
        SceneBuilder.SetRef(input, "rightButton", right);
    }

    // ── Settings: Controls selector, re-laid-out rows ────────────────

    static void SetupSettingsPanel(GameObject settings)
    {
        var panel = settings.GetComponent<SettingsPanel>();

        // Make room: title, sound, controls, tilt sensitivity, back
        Place(settings, "TitleText",          0f,  300f);
        Place(settings, "SoundLabel",      -100f,  170f);
        Place(settings, "SoundToggle",       80f,  170f);
        Place(settings, "SensLabel",          0f,  -90f);
        Place(settings, "SensitivitySlider", -30f, -160f);
        Place(settings, "SensValueText",    240f, -160f);
        Place(settings, "BackButton",         0f, -290f);

        if (settings.transform.Find("ControlsLabel") == null)
            SceneBuilder.TMPLabel(settings, "ControlsLabel", "Controls", 40,
                anchor: new Vector2(0.5f, 0.5f), pos: new Vector2(0f, 80f), size: new Vector2(600f, 60f))
                .alignment = TextAlignmentOptions.Center;

        var tilt    = GetOrCreateButton(settings, "TiltModeButton",    "Tilt",    new Vector2(-240f, 0f), new Vector2(220f, 80f));
        var touch   = GetOrCreateButton(settings, "TouchModeButton",   "Touch",   new Vector2(   0f, 0f), new Vector2(220f, 80f));
        var buttons = GetOrCreateButton(settings, "ButtonsModeButton", "Buttons", new Vector2( 240f, 0f), new Vector2(220f, 80f));

        SetOnlyListener(tilt,    panel.SelectTilt);
        SetOnlyListener(touch,   panel.SelectTouchSides);
        SetOnlyListener(buttons, panel.SelectButtons);

        SceneBuilder.SetRef(panel, "tiltModeButton",    tilt);
        SceneBuilder.SetRef(panel, "touchModeButton",   touch);
        SceneBuilder.SetRef(panel, "buttonsModeButton", buttons);

        var so  = new SerializedObject(panel);
        var arr = so.FindProperty("tiltOnly");
        string[] tiltOnly = { "SensLabel", "SensitivitySlider", "SensValueText" };
        arr.arraySize = tiltOnly.Length;
        for (int i = 0; i < tiltOnly.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = settings.transform.Find(tiltOnly[i])?.gameObject;
        so.ApplyModifiedProperties();
    }

    // ── HUD: hold-to-move buttons in the bottom corners ──────────────

    static (HoldButton, HoldButton) SetupMoveButtons(GameObject hud)
    {
        var left  = GetOrCreateMoveButton(hud, "MoveLeftButton",  LeftArrow,  corner: new Vector2(0f, 0f), offset: new Vector2( 40f, 40f));
        var right = GetOrCreateMoveButton(hud, "MoveRightButton", RightArrow, corner: new Vector2(1f, 0f), offset: new Vector2(-40f, 40f));
        return (left, right);
    }

    static HoldButton GetOrCreateMoveButton(GameObject hud, string name, string arrow, Vector2 corner, Vector2 offset)
    {
        var t = hud.transform.Find(name);
        var go = t != null ? t.gameObject : SceneBuilder.CreateButton(hud, name, arrow, Vector2.zero, Vector2.zero).gameObject;

        var rt = (RectTransform)go.transform;
        Undo.RecordObject(rt, "Controls UI");
        rt.anchorMin = rt.anchorMax = corner;   // bottom-left / bottom-right, inside the SafeArea'd HUD
        rt.pivot = corner;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(240f, 200f);

        var label = go.GetComponentInChildren<TMP_Text>(true);
        label.text = arrow;
        label.fontSize = 150;

        if (!go.TryGetComponent(out HoldButton hold))
            hold = Undo.AddComponent<HoldButton>(go);

        go.SetActive(false);   // ControlInput shows them in Buttons mode during a run
        return hold;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    static Button GetOrCreateButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.GetComponent<Button>();
        return SceneBuilder.CreateButton(parent, name, label, pos, size);
    }

    static void Place(GameObject parent, string child, float x, float y)
    {
        var t = parent.transform.Find(child) as RectTransform;
        if (t == null) return;
        Undo.RecordObject(t, "Controls UI");
        t.anchoredPosition = new Vector2(x, y);
    }

    static void SetOnlyListener(Button button, UnityAction action)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }
}
