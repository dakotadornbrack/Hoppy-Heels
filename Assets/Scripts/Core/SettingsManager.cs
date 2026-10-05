using UnityEngine;

/// <summary>
/// Persists player settings (sound on/off, tilt sensitivity) via PlayerPrefs.
/// Singleton — lives on the GameManager GameObject.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    const string KeySoundOn = "HoppyHeels_SoundOn";
    const string KeyTiltSensitivity = "HoppyHeels_TiltSensitivity";

    /// <summary>Default tilt multiplier matching PlayerController's default.</summary>
    const float DefaultSensitivity = 22f;
    const float MinSensitivity = 10f;
    const float MaxSensitivity = 40f;

    public bool SoundOn { get; private set; } = true;
    public float TiltSensitivity { get; private set; } = DefaultSensitivity;

    public static float MinSens => MinSensitivity;
    public static float MaxSens => MaxSensitivity;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Load();
        ApplySound();
    }

    public void SetSoundOn(bool on)
    {
        SoundOn = on;
        ApplySound();
        Save();
    }

    public void SetTiltSensitivity(float value)
    {
        TiltSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        Save();
    }

    void ApplySound()
    {
        AudioListener.volume = SoundOn ? 1f : 0f;
    }

    void Save()
    {
        PlayerPrefs.SetInt(KeySoundOn, SoundOn ? 1 : 0);
        PlayerPrefs.SetFloat(KeyTiltSensitivity, TiltSensitivity);
        PlayerPrefs.Save();
    }

    void Load()
    {
        SoundOn = PlayerPrefs.GetInt(KeySoundOn, 1) == 1;
        TiltSensitivity = PlayerPrefs.GetFloat(KeyTiltSensitivity, DefaultSensitivity);
    }
}
