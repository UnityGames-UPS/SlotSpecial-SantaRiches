using System;
using UnityEngine;

/// <summary>
/// Persists separate music and sound-effect settings and provides optional
/// bottom-panel sound hooks. Audio clips are intentionally optional because
/// this project currently contains no imported audio assets.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicVolumeKey = "SantaRiches.MusicVolume";
    private const string SfxVolumeKey = "SantaRiches.SfxVolume";
    private const string MusicEnabledKey = "SantaRiches.MusicEnabled";
    private const string SfxEnabledKey = "SantaRiches.SfxEnabled";

    [Header("Sources")]
    [Tooltip("Optional looping music source.")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Optional source used for one-shot interface sounds.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Optional UI Clips")]
    [SerializeField] private AudioClip normalClickClip;
    [SerializeField] private AudioClip turboClickClip;
    [SerializeField] private AudioClip maxBetClip;
    [SerializeField] private AudioClip popupOpenClip;
    [SerializeField] private AudioClip spinClickClip;

    internal event Action SettingsChanged;

    internal float MusicVolume { get; private set; } = 1f;
    internal float SfxVolume { get; private set; } = 1f;
    internal bool MusicEnabled { get; private set; } = true;
    internal bool SfxEnabled { get; private set; } = true;

    private void Awake()
    {
        LoadSettings();
        ApplySettings(false);
    }

    internal void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplySettings();
    }

    internal void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        ApplySettings();
    }

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;
        ApplySettings();
    }

    internal void SetSfxEnabled(bool enabled)
    {
        SfxEnabled = enabled;
        ApplySettings();
    }

    internal void PlayNormalClick() => PlaySfx(normalClickClip);
    internal void PlayTurboClick() => PlaySfx(turboClickClip);
    internal void PlayMaxBet() => PlaySfx(maxBetClip);
    internal void PlayPopupOpen() => PlaySfx(popupOpenClip);
    internal void PlaySpinClick() => PlaySfx(spinClickClip != null ? spinClickClip : normalClickClip);

    internal void PlaySfx(AudioClip clip)
    {
        if (!SfxEnabled || SfxVolume <= 0f || sfxSource == null || clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, SfxVolume);
    }

    private void LoadSettings()
    {
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        MusicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) != 0;
        SfxEnabled = PlayerPrefs.GetInt(SfxEnabledKey, 1) != 0;
    }

    private void ApplySettings(bool persist = true)
    {
        if (musicSource != null)
        {
            musicSource.volume = MusicEnabled ? MusicVolume : 0f;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = SfxEnabled ? SfxVolume : 0f;
        }

        if (persist)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            PlayerPrefs.SetInt(MusicEnabledKey, MusicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(SfxEnabledKey, SfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        SettingsChanged?.Invoke();
    }
}
