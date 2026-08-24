using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns the Santa Riches music and sound-effect channels, persists the user's
/// audio settings, and exposes event-specific playback methods to the game.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicVolumeKey = "SantaRiches.MusicVolume";
    private const string SfxVolumeKey = "SantaRiches.SfxVolume";
    private const string MusicEnabledKey = "SantaRiches.MusicEnabled";
    private const string SfxEnabledKey = "SantaRiches.SfxEnabled";

    [Header("Sources (Created Automatically When Empty)")]
    [SerializeField] private AudioSource musicSource;
    [FormerlySerializedAs("sfxSource")]
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource gameplaySource;
    [SerializeField] private AudioSource anticipationSource;

    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip freeGamesMusicClip;

    [Header("UI Sounds")]
    [FormerlySerializedAs("normalClickClip")]
    [SerializeField] private AudioClip generalButtonClip;
    [SerializeField] private AudioClip popupOpenClip;
    [SerializeField] private AudioClip betButtonClip;
    [FormerlySerializedAs("maxBetClip")]
    [SerializeField] private AudioClip maximumBetClip;
    [FormerlySerializedAs("turboClickClip")]
    [SerializeField] private AudioClip turboButtonClip;
    [SerializeField] private AudioClip freeSpinButtonClip;

    [Header("Reel and Feature Sounds")]
    [SerializeField] private AudioClip reelStopClip;
    [SerializeField] private AudioClip moonLandClip;
    [SerializeField] private AudioClip moonScatterClip;
    [SerializeField] private AudioClip giftRevealClip;
    [SerializeField] private AudioClip anticipationClip;

    [Header("Win Sounds")]
    [SerializeField] private AudioClip winningSymbolsClip;
    [SerializeField] private AudioClip winPaylineClip;
    [SerializeField] private AudioClip bigWinClip;
    [SerializeField] private AudioClip superBigWinClip;

    internal event Action SettingsChanged;

    internal float MusicVolume { get; private set; } = 1f;
    internal float SfxVolume { get; private set; } = 1f;
    internal bool MusicEnabled { get; private set; } = true;
    internal bool SfxEnabled { get; private set; } = true;

    private readonly List<AudioSource> allSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, bool> preFocusMuteState = new Dictionary<AudioSource, bool>();
    private bool isForceMuted;
    private bool useFreeGamesMusic;
    private Coroutine anticipationFadeCoroutine;

    private void Awake()
    {
        EnsureAudioSources();
        RefreshAudioSources();
        LoadSettings();
        ApplySettings(false);
    }

    private void OnApplicationFocus(bool focus)
    {
        SetMuteAll(!focus);
    }

    private void OnApplicationPause(bool paused)
    {
        SetMuteAll(paused);
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
                source.mute = preFocusMuteState.TryGetValue(source, out bool wasMuted) && wasMuted;
            }
        }

        if (!forceMute)
        {
            preFocusMuteState.Clear();
        }
    }

    internal void PlayBackgroundMusic()
    {
        useFreeGamesMusic = false;
        PlaySelectedMusic();
    }

    internal void PlayFreeGamesMusic()
    {
        useFreeGamesMusic = true;
        PlaySelectedMusic();
    }

    private void PlaySelectedMusic()
    {
        AudioClip selectedClip = useFreeGamesMusic && freeGamesMusicClip != null
            ? freeGamesMusicClip
            : backgroundMusicClip;
        if (!MusicEnabled || MusicVolume <= 0f || musicSource == null || selectedClip == null)
        {
            return;
        }

        if (musicSource.isPlaying && musicSource.clip == selectedClip)
        {
            return;
        }

        musicSource.clip = selectedClip;
        musicSource.loop = true;
        musicSource.Play();
    }

    internal void StopBackgroundMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    internal void PlayNormalClick() => PlayUiSound(generalButtonClip);
    internal void PlayPopupOpen() => PlayUiSound(popupOpenClip);
    internal void PlayBetChange() => PlayUiSound(betButtonClip);
    internal void PlayMaxBet() => PlayUiSound(maximumBetClip);
    internal void PlayTurboClick() => PlayUiSound(turboButtonClip);
    internal void PlayFreeSpinButton() => PlayUiSound(freeSpinButtonClip);

    internal void PlayReelStop() => PlayGameplaySound(reelStopClip);
    internal void PlayMoonLand() => PlayGameplaySound(moonLandClip);
    internal void PlayMoonScatter() => PlayGameplaySound(moonScatterClip);
    internal void PlayGiftReveal() => PlayGameplaySound(giftRevealClip);
    internal void PlayWinningSymbols() => PlayGameplaySound(winningSymbolsClip);
    internal void PlayWinPayline() => PlayGameplaySound(winPaylineClip);

    internal void PlayAnticipation()
    {
        CancelAnticipationFade();

        if (!SfxEnabled || SfxVolume <= 0f || isForceMuted ||
            anticipationSource == null || anticipationClip == null)
        {
            return;
        }

        if (anticipationSource.isPlaying && anticipationSource.clip == anticipationClip)
        {
            anticipationSource.volume = SfxVolume;
            return;
        }

        anticipationSource.Stop();
        anticipationSource.volume = SfxVolume;
        anticipationSource.clip = anticipationClip;
        anticipationSource.loop = true;
        anticipationSource.Play();
    }

    internal void FadeOutAnticipation(float duration)
    {
        if (anticipationSource == null || !anticipationSource.isPlaying)
        {
            return;
        }

        CancelAnticipationFade();
        if (duration <= 0f)
        {
            StopAnticipation();
            return;
        }

        anticipationFadeCoroutine = StartCoroutine(FadeOutAnticipationRoutine(duration));
    }

    internal void StopAnticipation()
    {
        CancelAnticipationFade();

        if (anticipationSource == null)
        {
            return;
        }

        anticipationSource.Stop();
        anticipationSource.clip = null;
        anticipationSource.volume = SfxEnabled ? SfxVolume : 0f;
    }

    private IEnumerator FadeOutAnticipationRoutine(float duration)
    {
        float startVolume = anticipationSource.volume;
        float elapsed = 0f;
        while (anticipationSource != null && anticipationSource.isPlaying && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            anticipationSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (anticipationSource != null)
        {
            anticipationSource.Stop();
            anticipationSource.clip = null;
            anticipationSource.volume = SfxEnabled ? SfxVolume : 0f;
        }

        anticipationFadeCoroutine = null;
    }

    private void CancelAnticipationFade()
    {
        if (anticipationFadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(anticipationFadeCoroutine);
        anticipationFadeCoroutine = null;
    }

    internal void PlayExtraWin(WinPopupType popupType)
    {
        AudioClip clip = popupType == WinPopupType.SuperBigWin
            ? superBigWinClip
            : popupType == WinPopupType.BigWin
                ? bigWinClip
                : null;
        PlayGameplaySound(clip);
    }

    internal void PlaySfx(AudioClip clip)
    {
        PlayGameplaySound(clip);
    }

    private void PlayUiSound(AudioClip clip)
    {
        PlayOneShot(uiSource, clip);
    }

    private void PlayGameplaySound(AudioClip clip)
    {
        PlayOneShot(gameplaySource, clip);
    }

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (!SfxEnabled || SfxVolume <= 0f || isForceMuted || source == null || clip == null)
        {
            return;
        }

        source.PlayOneShot(clip);
    }

    private void EnsureAudioSources()
    {
        musicSource = EnsureAudioSource(musicSource, true);
        uiSource = EnsureAudioSource(uiSource, false);
        gameplaySource = EnsureAudioSource(gameplaySource, false);
        anticipationSource = EnsureAudioSource(anticipationSource, true);
    }

    private AudioSource EnsureAudioSource(AudioSource source, bool shouldLoop)
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = shouldLoop;
        source.spatialBlend = 0f;
        return source;
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
            source.mute = preFocusMuteState.TryGetValue(source, out bool wasMuted) && wasMuted;
        }
        preFocusMuteState.Clear();
    }

    private void RefreshAudioSources()
    {
        allSources.RemoveAll(source => source == null);
        AddAudioSource(musicSource);
        AddAudioSource(uiSource);
        AddAudioSource(gameplaySource);
        AddAudioSource(anticipationSource);

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

        float sfxVolume = SfxEnabled ? SfxVolume : 0f;
        if (uiSource != null) uiSource.volume = sfxVolume;
        if (gameplaySource != null) gameplaySource.volume = sfxVolume;
        if (anticipationSource != null) anticipationSource.volume = sfxVolume;

        if (!SfxEnabled || SfxVolume <= 0f)
        {
            StopAnticipation();
        }

        if (MusicEnabled && MusicVolume > 0f)
        {
            PlaySelectedMusic();
        }
        else
        {
            StopBackgroundMusic();
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
