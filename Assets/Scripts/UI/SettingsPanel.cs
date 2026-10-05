using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings panel UI — sound toggle and tilt sensitivity slider.
/// Accessible from both Main Menu and Pause Menu.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] Toggle soundToggle;
    [SerializeField] Slider sensitivitySlider;
    [SerializeField] TMP_Text sensitivityValueText;

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
    }

    void OnDisable()
    {
        if (soundToggle != null)
            soundToggle.onValueChanged.RemoveListener(OnSoundToggled);
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
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
