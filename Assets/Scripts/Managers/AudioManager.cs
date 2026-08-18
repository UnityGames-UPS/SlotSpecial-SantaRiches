using System;
using System.Collections.Generic;
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

    private readonly List<AudioSource> allSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, bool> preFocusMuteState = new Dictionary<AudioSource, bool>();
    private bool isForceMuted;

    private void Awake()
    {
        RefreshAudioSources();
        LoadSettings();
        ApplySettings(false);
    }

    private void OnApplicationFocus(bool focus)
    {
        SetMuteAll(!focus);
    }

    internal void SetMusicVolume(float value)
    {
        ClearForcedMuteForUserInteraction();
        MusicVolume = Mathf.Clamp01(value);
        ApplySettings();
    }

    internal void SetSfxVolume(float value)
    {
        ClearForcedMuteForUserInteraction();
        SfxVolume = Mathf.Clamp01(value);
        ApplySettings();
    }

    internal void SetMusicEnabled(bool enabled)
    {
        ClearForcedMuteForUserInteraction();
        MusicEnabled = enabled;
        ApplySettings();
    }

    internal void SetSfxEnabled(bool enabled)
    {
        ClearForcedMuteForUserInteraction();
        SfxEnabled = enabled;
        ApplySettings();
    }

    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;
        RefreshAudioSources();

        foreach (AudioSource source in allSources)
        {
            if (source == null) continue;

            if (forceMute)
            {
                preFocusMuteState[source] = source.mute;
                source.mute = true;
            }
            else
            {
                source.mute = preFocusMuteState.TryGetValue(source, out bool wasMuted)
                    ? wasMuted
                    : source.mute;
            }
        }

        if (!forceMute)
        {
            preFocusMuteState.Clear();
        }
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

    private void ClearForcedMuteForUserInteraction()
    {
        if (!isForceMuted) return;

        isForceMuted = false;
        RefreshAudioSources();
        foreach (AudioSource source in allSources)
        {
            if (source == null) continue;
            source.mute = preFocusMuteState.TryGetValue(source, out bool wasMuted)
                ? wasMuted
                : source.mute;
        }
        preFocusMuteState.Clear();
    }

    private void RefreshAudioSources()
    {
        allSources.RemoveAll(source => source == null);
        AddAudioSource(musicSource);
        AddAudioSource(sfxSource);

        foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>())
        {
            if (source != null && source.gameObject.scene.IsValid())
            {
                AddAudioSource(source);
            }
        }
    }

    private void AddAudioSource(AudioSource source)
    {
        if (source != null && !allSources.Contains(source))
        {
            allSources.Add(source);
        }
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
