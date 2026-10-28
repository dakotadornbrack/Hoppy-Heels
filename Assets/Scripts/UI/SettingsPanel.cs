using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings panel UI — sound toggle, control mode (Tilt / Touch / Buttons) and
/// tilt sensitivity slider (only shown for Tilt).
/// Accessible from both Main Menu and Pause Menu.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] Toggle soundToggle;
    [SerializeField] Slider sensitivitySlider;
    [SerializeField] TMP_Text sensitivityValueText;

    [Header("Control mode")]
    [SerializeField] Button tiltModeButton;
    [SerializeField] Button touchModeButton;
    [SerializeField] Button buttonsModeButton;
    [Tooltip("Hidden unless Tilt is selected (the sensitivity label, slider and value)")]
    [SerializeField] GameObject[] tiltOnly;
    [SerializeField] Color selectedColor   = new Color(1.00f, 0.18f, 0.62f);   // neon pink
    [SerializeField] Color unselectedColor = new Color(0.45f, 0.20f, 0.95f);   // violet

    void OnEnable()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        if (soundToggle != null)
        {
            soundToggle.isOn = sm.SoundOn;
            soundToggle.onValueChanged.AddListener(OnSoundToggled);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = SettingsManager.MinSens;
            sensitivitySlider.maxValue = SettingsManager.MaxSens;
            sensitivitySlider.value = sm.TiltSensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            UpdateSensitivityLabel(sm.TiltSensitivity);
        }

        RefreshControlMode();
    }

    void OnDisable()
    {
        if (soundToggle != null)
            soundToggle.onValueChanged.RemoveListener(OnSoundToggled);
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
    }

    // Wired to the three mode buttons' onClick
    public void SelectTilt()       => SelectControlMode(ControlMode.Tilt);
    public void SelectTouchSides() => SelectControlMode(ControlMode.TouchSides);
    public void SelectButtons()    => SelectControlMode(ControlMode.Buttons);

    public void SelectControlMode(ControlMode mode)
    {
        SettingsManager.Instance?.SetControlMode(mode);
        RefreshControlMode();
    }

    void RefreshControlMode()
    {
        var mode = SettingsManager.Instance != null ? SettingsManager.Instance.Controls : ControlMode.Tilt;

        Highlight(tiltModeButton,    mode == ControlMode.Tilt);
        Highlight(touchModeButton,   mode == ControlMode.TouchSides);
        Highlight(buttonsModeButton, mode == ControlMode.Buttons);

        if (tiltOnly != null)
            foreach (var go in tiltOnly)
                if (go != null) go.SetActive(mode == ControlMode.Tilt);
    }

    void Highlight(Button button, bool selected)
    {
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = selected ? selectedColor : unselectedColor;
    }

    void OnSoundToggled(bool on)
    {
        SettingsManager.Instance?.SetSoundOn(on);
    }

    void OnSensitivityChanged(float value)
    {
        SettingsManager.Instance?.SetTiltSensitivity(value);
        UpdateSensitivityLabel(value);
    }

    void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = Mathf.RoundToInt(value).ToString();
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
