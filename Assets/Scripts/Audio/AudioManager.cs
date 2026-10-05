using UnityEngine;

/// <summary>
/// Central sound manager. Assign AudioClips in the Inspector — all slots are optional.
/// Call AudioManager.Instance.Play(SFX.Jump) etc. from anywhere.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum SFX { Jump, BounceBoost, BreakPlatform, Hazard, GameOver, CollectBean }

    [Header("Jump / Bounce")]
    [SerializeField] AudioClip jumpClip;
    [SerializeField] AudioClip bounceBoostClip;

    [Header("Platforms")]
    [SerializeField] AudioClip breakPlatformClip;
    [SerializeField] AudioClip hazardClip;

    [Header("Game Events")]
    [SerializeField] AudioClip gameOverClip;
    [SerializeField] AudioClip collectBeanClip;

    [Header("Volumes")]
    [SerializeField] [Range(0f, 1f)] float sfxVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] float musicVolume = 0.5f;

    AudioSource sfxSource;
    AudioSource musicSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource   = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();

        sfxSource.volume   = sfxVolume;
        musicSource.volume = musicVolume;
        musicSource.loop   = true;
    }

    public void Play(SFX sfx)
    {
        AudioClip clip = sfx switch
        {
            SFX.Jump          => jumpClip,
            SFX.BounceBoost   => bounceBoostClip,
            SFX.BreakPlatform => breakPlatformClip,
            SFX.Hazard        => hazardClip,
            SFX.GameOver      => gameOverClip,
            SFX.CollectBean   => collectBeanClip,
            _                 => null
        };

        if (clip != null)
            sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic() => musicSource.Stop();
}
