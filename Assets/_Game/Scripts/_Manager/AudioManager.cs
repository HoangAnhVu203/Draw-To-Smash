using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    void Awake()
    {
        Instance = this;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (!IsSoundOn()) return;

        sfxSource.PlayOneShot(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (!IsMusicOn()) return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    //---------------- SETTING ----------------//
    public static bool IsSoundOn() => PlayerPrefs.GetInt("SOUND_ON", 1) == 1;
    public static bool IsMusicOn() => PlayerPrefs.GetInt("MUSIC_ON", 1) == 1;
    public static bool IsVibrationOn() => PlayerPrefs.GetInt("VIBRATION_ON", 1) == 1;
}
