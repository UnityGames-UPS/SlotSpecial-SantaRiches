using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns all bottom-panel input and presentation. Gameplay commands go through
/// GameManager, while SlotBehaviour remains focused on reel presentation.
/// </summary>
[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    private const int InfiniteAutoplay = -1;
    private const int DecimalPointSpriteIndex = 10;
    private const int CommaSpriteIndex = 11;

    [Header("Controllers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private JSFunctCalls jsBridge;
    [SerializeField] private AudioManager audioManager;

    [Header("Game Values")]
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text betAmountText;

    [Header("Primary Controls")]
    [SerializeField] private Button spinButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button autoplayStopButton;
    [SerializeField] private TMP_Text autoplayCountText;
    [SerializeField] private Button betIncreaseButton;
    [SerializeField] private Button betDecreaseButton;
    [SerializeField] private Button normalSpeedButton;
    [SerializeField] private Button fastSpeedButton;
    [SerializeField] private Button skipSpeedButton;

    [Header("Free Spin UI")]
    [SerializeField] private GameObject freeSpinPanel;
    [SerializeField] private GameObject freeSpinCountPanel;
    [SerializeField] private GameObject freeSpinWinPanel;
    [SerializeField] private Button freeSpinPanelStartButton;
    [SerializeField] private Button bottomFreeSpinStartButton;
    [SerializeField] private Button takeFreeSpinButton;
    [SerializeField] private TMP_Text totalFreeSpinsText;
    [SerializeField] private TMP_Text remainingFreeSpinsText;
    [SerializeField] private TMP_Text freeSpinTotalWinText;
    [SerializeField] private StarFountain freeSpinStarFountain;
    [SerializeField, Min(0f)] private float freeSpinTotalWinCountDuration = 1.5f;

    [Header("Free Spin Offer Transition")]
    [SerializeField, Min(0f)] private float freeSpinFadeToBlackDuration = 0.35f;
    [SerializeField, Min(0f)] private float freeSpinFadeFromBlackDuration = 0.45f;
    [SerializeField, Min(0f)] private float freeSpinOfferScaleDuration = 0.5f;

    [Header("Autoplay Panel")]
    [SerializeField] private GameObject autoplayPanel;
    [SerializeField] private CanvasGroup autoplayCanvasGroup;
    [SerializeField, Min(0.1f)] private float spinHoldDuration = 0.75f;
    [SerializeField, Min(0f)] private float autoplaySlideDuration = 0.2f;
    [SerializeField, Min(1f)] private float autoplayClosedOffset = 640f;
    [SerializeField] private Button auto10Button;
    [SerializeField] private Button auto50Button;
    [SerializeField] private Button auto100Button;
    [SerializeField] private Button auto200Button;
    [SerializeField] private Button auto500Button;
    [SerializeField] private Button autoInfinityButton;

    [Header("Menu")]
    [SerializeField] private Button hamburgerButton;
    [SerializeField] private Button menuCloseButton;
    [SerializeField] private GameObject hamburgerIcon;
    [SerializeField] private GameObject downIcon;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField, Min(0f)] private float menuFadeDuration = 0.18f;

    [Header("Optional Full Panels")]
    [Tooltip("Paytable/information page root. It is optional until artwork is supplied.")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button infoBackButton;
    [Tooltip("Guide page root. It is optional until artwork is supplied.")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private Button guideButton;
    [SerializeField] private Button guideBackButton;

    [Header("Optional Sound Panel")]
    [SerializeField] private GameObject soundPanel;
    [SerializeField] private CanvasGroup soundCanvasGroup;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button soundCloseButton;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField, Min(0f)] private float soundTweenDuration = 0.2f;

    [Header("Platform Controls")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button moreGamesButton;
    [SerializeField] private Button enterFullscreenButton;
    [SerializeField] private Button exitFullscreenButton;
    [SerializeField] private bool moreGamesEnabled = true;
    [SerializeField] private string moreGamesMessage = "more_games";
    [SerializeField] private string enterFullscreenMessage = "enter_fullscreen";
    [SerializeField] private string exitFullscreenMessage = "exit_fullscreen";

    private EventTrigger spinEventTrigger;
    private EventTrigger.Entry spinPointerDownEntry;
    private EventTrigger.Entry spinPointerUpEntry;
    private EventTrigger.Entry spinPointerExitEntry;
    private Coroutine holdRoutine;
    private Tween autoplayTween;
    private Tween menuTween;
    private Tween soundTween;
    private Tween freeSpinOfferTransitionTween;
    private Tween freeSpinTotalWinTween;
    private CanvasGroup freeSpinTransitionOverlay;
    private Vector3 freeSpinPanelOriginalScale = Vector3.one;
    private bool freeSpinPanelScaleCached;
    private Vector2 autoplayOpenPosition;
    private bool autoplayPositionCached;
    private bool pointerHeld;
    private bool longPressTriggered;
    private bool suppressNextSpinClick;
    private bool waitForOpeningPointerRelease;
    private bool menuOpen;
    private bool soundOpen;
    private bool listenersRegistered;
    private bool fullscreenState;
    private bool lastSocketConnected;
    private bool lastPopupBlockingState;

    // Autoplay and hamburger panels are non-modal: their animations must not
    // disable the surrounding game controls. Sound and server/error popups
    // remain modal and continue to block background input.
    private bool IsBlockingInteraction =>
        soundOpen || (popupManager != null && popupManager.IsBlockingPopupActive);

    private void Awake()
    {
        ResolveReferences();
        if (jsBridge != null)
        {
            jsBridge.RegisterVisibilityListener(gameObject.name);
        }
        PreparePanels();
        ConfigureSpinPointerEvents();
    }

    private void OnEnable()
    {
        RegisterListeners();
        OrientationChange.OnOrientationChanged += HandleOrientationChanged;
        RefreshControls();
    }

    private void Start()
    {
        fullscreenState = Screen.fullScreen;
        lastSocketConnected = gameManager != null && gameManager.IsSocketConnected;
        lastPopupBlockingState = popupManager != null && popupManager.IsBlockingPopupActive;
        SyncSoundControls();
        RefreshControls();
    }

    private void OnDisable()
    {
        UnregisterListeners();
        OrientationChange.OnOrientationChanged -= HandleOrientationChanged;
        CancelSpinHold();
        KillPanelTweens();
    }

    private void Update()
    {
        bool currentFullscreen = Screen.fullScreen;
        if (currentFullscreen != fullscreenState)
        {
            fullscreenState = currentFullscreen;
            RefreshControls();
        }

        bool socketConnected = gameManager != null && gameManager.IsSocketConnected;
        bool popupBlocking = popupManager != null && popupManager.IsBlockingPopupActive;
        if (socketConnected != lastSocketConnected || popupBlocking != lastPopupBlockingState)
        {
            lastSocketConnected = socketConnected;
            lastPopupBlockingState = popupBlocking;
            RefreshControls();
        }

        if (waitForOpeningPointerRelease)
        {
            if (!IsAnyPrimaryPointerPressed()) waitForOpeningPointerRelease = false;
            return;
        }

        if (!TryGetNewPointerDown(out Vector2 screenPosition)) return;

        if (autoplayPanel != null && autoplayPanel.activeSelf && !IsAutoplayDismissalException(screenPosition))
        {
            HideAutoplayPanel();
        }

        if (menuOpen && !IsPointerInside(menuPanel, screenPosition) &&
            !IsPointerInside(hamburgerButton != null ? hamburgerButton.gameObject : null, screenPosition) &&
            !IsPointerInside(menuCloseButton != null ? menuCloseButton.gameObject : null, screenPosition))
        {
            CloseMenu();
        }
    }

    public void OnFocusChanged(string value)
    {
        bool focused = value == "1";
        Debug.Log("UNITY FOCUS CHANGED: " + value + " (focused: " + focused + ")");
        audioManager?.SetMuteAll(!focused);
        socketManager?.HandleFocusChange(focused);
    }

    internal void UpdatePingDisplay(string value)
    {
        TMP_Text pingText = FindNamedComponent<TMP_Text>("PingText");
        if (pingText != null) pingText.text = value;
    }

    internal void UpdatePingDisplay(int milliseconds)
    {
        UpdatePingDisplay($"{milliseconds} ms");
    }

    internal void UpdateJackpotDisplay(JackpotValues values)
    {
        // Jackpot presentation is not part of the existing bottom-panel artwork.
    }

    internal void UpdateBalanceDisplay()
    {
        RefreshControls();
    }

    internal void ShowFreeSpinOffer(int totalSpins)
    {
        CancelFreeSpinTotalWinCount();
        StopFreeSpinStarFountain();
        if (freeSpinCountPanel != null) freeSpinCountPanel.SetActive(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        SetVisible(bottomFreeSpinStartButton, true);
        SetVisible(takeFreeSpinButton, false);
        UpdateFreeSpinCounter(totalSpins, totalSpins);
        PlayFreeSpinOfferTransition();
        RefreshControls();
    }

    internal void BeginFreeSpinPresentation(int totalSpins, int remainingSpins)
    {
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
        if (freeSpinCountPanel != null) freeSpinCountPanel.SetActive(true);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(takeFreeSpinButton, false);
        UpdateFreeSpinCounter(totalSpins, remainingSpins);
        RefreshControls();
    }

    internal void UpdateFreeSpinCounter(int totalSpins, int remainingSpins)
    {
        if (totalFreeSpinsText != null)
        {
            totalFreeSpinsText.text = FormatSpriteInteger(totalFreeSpinsText, totalSpins);
        }

        if (remainingFreeSpinsText != null)
        {
            remainingFreeSpinsText.text = FormatSpriteInteger(remainingFreeSpinsText, remainingSpins);
        }
    }

    private static string FormatSpriteInteger(TMP_Text target, int value)
    {
        string plainText = Mathf.Max(0, value).ToString();
        return ConvertTextToSprites(target, plainText);
    }

    private static string FormatSpriteAmount(TMP_Text target, double amount, int decimalPlaces)
    {
        int safeDecimalPlaces = Math.Max(0, Math.Min(28, decimalPlaces));
        string format = safeDecimalPlaces == 0
            ? "#,0"
            : "#,0." + new string('0', safeDecimalPlaces);
        string plainText = Math.Max(0d, amount).ToString(format, CultureInfo.InvariantCulture);
        return ConvertTextToSprites(target, plainText);
    }

    private static string ConvertTextToSprites(TMP_Text target, string plainText)
    {
        TMP_SpriteAsset spriteAsset = target != null ? target.spriteAsset : null;
        if (spriteAsset == null || spriteAsset.spriteCharacterTable == null)
        {
            return plainText;
        }

        var spriteText = new System.Text.StringBuilder(plainText.Length * 10);
        foreach (char character in plainText)
        {
            int spriteIndex = GetNumberSpriteIndex(character);
            if (spriteIndex < 0 || spriteIndex >= spriteAsset.spriteCharacterTable.Count ||
                spriteAsset.spriteCharacterTable[spriteIndex] == null)
            {
                return plainText;
            }

            spriteText.Append("<sprite=");
            spriteText.Append(spriteIndex);
            spriteText.Append('>');
        }

        return spriteText.ToString();
    }

    private static int GetNumberSpriteIndex(char character)
    {
        if (character >= '0' && character <= '9') return character - '0';
        if (character == '.') return DecimalPointSpriteIndex;
        if (character == ',') return CommaSpriteIndex;
        return -1;
    }

    internal void ShowFreeSpinCompletion(string formattedTotalWin)
    {
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
        if (freeSpinCountPanel != null) freeSpinCountPanel.SetActive(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(true);
        StartFreeSpinTotalWinCount(formattedTotalWin);
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(takeFreeSpinButton, true);
        RefreshControls();
    }

    private void StartFreeSpinTotalWinCount(string formattedTotalWin)
    {
        CancelFreeSpinTotalWinCount();
        if (freeSpinTotalWinText == null)
        {
            return;
        }

        string finalText = string.IsNullOrWhiteSpace(formattedTotalWin) ? "0" : formattedTotalWin.Trim();
        if (!double.TryParse(finalText, NumberStyles.Number, CultureInfo.InvariantCulture, out double finalAmount))
        {
            freeSpinTotalWinText.text = ConvertTextToSprites(freeSpinTotalWinText, finalText);
            return;
        }

        finalAmount = Math.Max(0d, finalAmount);
        int decimalPlaces = GetFormattedDecimalPlaces(finalText);
        freeSpinTotalWinText.text = FormatSpriteAmount(freeSpinTotalWinText, 0d, decimalPlaces);

        if (finalAmount <= 0d || freeSpinTotalWinCountDuration <= 0f)
        {
            freeSpinTotalWinText.text = FormatSpriteAmount(freeSpinTotalWinText, finalAmount, decimalPlaces);
            return;
        }

        double displayedAmount = 0d;
        freeSpinTotalWinTween = DOTween.To(
                () => displayedAmount,
                value =>
                {
                    displayedAmount = value;
                    if (freeSpinTotalWinText != null)
                    {
                        freeSpinTotalWinText.text = FormatSpriteAmount(
                            freeSpinTotalWinText,
                            displayedAmount,
                            decimalPlaces);
                    }
                },
                finalAmount,
                freeSpinTotalWinCountDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (freeSpinTotalWinText != null)
                {
                    freeSpinTotalWinText.text = FormatSpriteAmount(
                        freeSpinTotalWinText,
                        finalAmount,
                        decimalPlaces);
                }

                freeSpinTotalWinTween = null;
            });
    }

    private static int GetFormattedDecimalPlaces(string formattedAmount)
    {
        int decimalPoint = formattedAmount.LastIndexOf('.');
        return decimalPoint < 0
            ? 0
            : Mathf.Clamp(formattedAmount.Length - decimalPoint - 1, 0, 28);
    }

    private void CancelFreeSpinTotalWinCount()
    {
        freeSpinTotalWinTween?.Kill();
        freeSpinTotalWinTween = null;
    }

    internal void ResetFreeSpinPresentation()
    {
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
        if (freeSpinCountPanel != null) freeSpinCountPanel.SetActive(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(takeFreeSpinButton, false);
        RefreshControls();
    }

    /// <summary>Callable by the WebGL host when fullscreen changes externally.</summary>
    internal void SetFullscreenStateFromHost(string value)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            fullscreenState = parsed;
            RefreshControls();
        }
    }

    internal void RefreshControls()
    {
        if (gameManager == null) return;

        bool blocked = IsBlockingInteraction;
        bool autoplay = gameManager.IsAutoplayActive;
        bool settlingAutoplay = gameManager.IsAutoplayRoundSettling;
        bool freeSpinOffer = gameManager.IsFreeSpinAwaitingStart;
        bool freeSpinActive = gameManager.IsFreeSpinActive;
        bool freeSpinAwaitingTake = gameManager.IsFreeSpinAwaitingTake;
        bool manualSpin = gameManager.IsCurrentlySpinning && !autoplay && !settlingAutoplay && !freeSpinActive;
        bool showingResult = gameManager.IsResultPresentationActive && !autoplay;
        bool extraGiftWildReveal = gameManager.IsExtraGiftWildRevealActive;
        bool showStop = manualSpin && !extraGiftWildReveal;
        bool showAutoplayStop = autoplay && !freeSpinOffer && !freeSpinActive;
        bool showSpin = !freeSpinOffer && !showStop && !showAutoplayStop;

        SetVisible(spinButton, showSpin);
        SetVisible(stopButton, showStop);
        SetVisible(autoplayStopButton, showAutoplayStop);
        SetVisible(bottomFreeSpinStartButton, freeSpinOffer);
        SetVisible(takeFreeSpinButton, freeSpinAwaitingTake);

        if (spinButton != null)
        {
            spinButton.interactable = showSpin && !extraGiftWildReveal && !freeSpinActive && !blocked &&
                !settlingAutoplay && !showingResult && gameManager.CanAttemptManualSpin;
        }

        if (stopButton != null)
        {
            stopButton.interactable = showStop && !blocked && !gameManager.IsStopRequested;
        }

        if (autoplayStopButton != null)
        {
            autoplayStopButton.interactable = showAutoplayStop && !blocked;
        }

        if (bottomFreeSpinStartButton != null)
        {
            bottomFreeSpinStartButton.interactable = freeSpinOffer && !blocked;
        }

        if (freeSpinPanelStartButton != null)
        {
            freeSpinPanelStartButton.interactable = freeSpinOffer && !blocked;
        }

        if (takeFreeSpinButton != null)
        {
            takeFreeSpinButton.interactable = freeSpinAwaitingTake && !blocked;
        }

        if (autoplayCountText != null)
        {
            autoplayCountText.gameObject.SetActive(autoplay);
            autoplayCountText.text = gameManager.AutoplaySpinsRemaining < 0
                ? "\u221E"
                : gameManager.AutoplaySpinsRemaining.ToString();
        }

        UpdateAmountText(balanceText, gameManager.CurrentBalance);
        UpdateAmountText(betAmountText, gameManager.CurrentTotalBet);

        bool allowBetChange = !blocked && gameManager.CanChangeBet;
        SetInteractable(betIncreaseButton, allowBetChange);
        SetInteractable(betDecreaseButton, allowBetChange);

        SpinSpeed speed = gameManager.CurrentSpinSpeed;
        bool allowSpeedChange = !blocked && gameManager.CanChangeSpinSpeed;
        SetVisible(normalSpeedButton, speed == SpinSpeed.Normal);
        SetVisible(fastSpeedButton, speed == SpinSpeed.Turbo);
        SetVisible(skipSpeedButton, speed == SpinSpeed.QuickSpin);
        SetInteractable(normalSpeedButton, allowSpeedChange);
        SetInteractable(fastSpeedButton, allowSpeedChange);
        SetInteractable(skipSpeedButton, allowSpeedChange);

        SetInteractable(hamburgerButton, !blocked && !menuOpen);
        SetInteractable(menuCloseButton, !blocked && menuOpen);
        SetInteractable(homeButton, !blocked && !autoplay && !freeSpinOffer && !freeSpinActive);
        SetInteractable(moreGamesButton, !blocked && moreGamesEnabled && !freeSpinOffer && !freeSpinActive);
        SetInteractable(enterFullscreenButton, !blocked);
        SetInteractable(exitFullscreenButton, !blocked);

        if (enterFullscreenButton != null) enterFullscreenButton.gameObject.SetActive(!fullscreenState);
        if (exitFullscreenButton != null) exitFullscreenButton.gameObject.SetActive(fullscreenState);

        bool autoplayChoicesEnabled = !blocked && gameManager.CanStartManualSpin;
        SetInteractable(auto10Button, autoplayChoicesEnabled);
        SetInteractable(auto50Button, autoplayChoicesEnabled);
        SetInteractable(auto100Button, autoplayChoicesEnabled);
        SetInteractable(auto200Button, autoplayChoicesEnabled);
        SetInteractable(auto500Button, autoplayChoicesEnabled);
        SetInteractable(autoInfinityButton, autoplayChoicesEnabled);
    }

    private void ResolveReferences()
    {
        gameManager = gameManager != null ? gameManager : FindSceneComponent<GameManager>();
        socketManager = socketManager != null ? socketManager : FindSceneComponent<SocketIOManager>();
        popupManager = popupManager != null ? popupManager : FindSceneComponent<PopupManager>();
        jsBridge = jsBridge != null ? jsBridge : FindSceneComponent<JSFunctCalls>();
        audioManager = audioManager != null ? audioManager : FindSceneComponent<AudioManager>();

        if (audioManager == null)
        {
            GameObject audioObject = FindSceneObject("AudioManager");
            if (audioObject != null) audioManager = audioObject.AddComponent<AudioManager>();
        }

        balanceText = balanceText != null ? balanceText : FindNamedComponent<TMP_Text>("BalanceAmount");
        betAmountText = betAmountText != null ? betAmountText : FindNamedComponent<TMP_Text>("BetAmount");

        spinButton = ResolveButton(spinButton, "Spin");
        stopButton = ResolveButton(stopButton, "Stop");
        autoplayStopButton = ResolveButton(autoplayStopButton, "AutoplayStop");
        autoplayCountText = autoplayCountText != null ? autoplayCountText : FindNamedComponent<TMP_Text>("AutoplayCount");
        betIncreaseButton = ResolveButton(betIncreaseButton, "BetIncrease");
        betDecreaseButton = ResolveButton(betDecreaseButton, "BetDecrease");
        normalSpeedButton = ResolveButton(normalSpeedButton, "NormalSpinSpeed");
        fastSpeedButton = ResolveButton(fastSpeedButton, "FastSpinSpeed");
        skipSpeedButton = ResolveButton(skipSpeedButton, "SkipSpinSpeed");

        freeSpinPanel = freeSpinPanel != null ? freeSpinPanel : FindSceneObject("FreeSpinPanel");
        freeSpinCountPanel = freeSpinCountPanel != null ? freeSpinCountPanel : FindSceneObject("FreeSpinCountPanel");
        freeSpinWinPanel = freeSpinWinPanel != null ? freeSpinWinPanel : FindSceneObject("FreeSpinWinPanel");
        freeSpinPanelStartButton = ResolveChildButton(freeSpinPanelStartButton, freeSpinPanel, "Start");
        bottomFreeSpinStartButton = bottomFreeSpinStartButton != null
            ? bottomFreeSpinStartButton
            : FindBottomFreeSpinStartButton();
        Button collectButton = ResolveChildButton(null, freeSpinWinPanel, "Collect", "COLLECT");
        takeFreeSpinButton = collectButton != null
            ? collectButton
            : ResolveButton(takeFreeSpinButton, "Take", "TAKE", "Collect", "COLLECT");
        totalFreeSpinsText = ResolveChildComponent(totalFreeSpinsText, freeSpinCountPanel, "TotalFreeSpins");
        remainingFreeSpinsText = ResolveChildComponent(
            remainingFreeSpinsText,
            freeSpinCountPanel,
            "RemainingFreeSpins",
            "RemainingFreeSpin");
        freeSpinTotalWinText = ResolveChildComponent(
            freeSpinTotalWinText,
            freeSpinWinPanel,
            "TotalWin");
        freeSpinStarFountain = freeSpinStarFountain != null
            ? freeSpinStarFountain
            : FindSceneComponent<StarFountain>();

        autoplayPanel = autoplayPanel != null ? autoplayPanel : FindSceneObject("Autoplay Panel");
        menuPanel = menuPanel != null ? menuPanel : FindSceneObject("HamburgerMenu");
        hamburgerButton = ResolveButton(hamburgerButton, "HamburgerIcon");
        menuCloseButton = ResolveButton(menuCloseButton, "DownIcon");
        hamburgerIcon = hamburgerIcon != null ? hamburgerIcon : hamburgerButton != null ? hamburgerButton.gameObject : null;
        downIcon = downIcon != null ? downIcon : menuCloseButton != null ? menuCloseButton.gameObject : null;

        infoButton = ResolveButton(infoButton, "Info");
        guideButton = ResolveButton(guideButton, "Guide", "Bulb");
        soundButton = ResolveButton(soundButton, "Sound");
        homeButton = ResolveButton(homeButton, "Home");
        moreGamesButton = ResolveButton(moreGamesButton, "MoreGames");
        enterFullscreenButton = ResolveButton(enterFullscreenButton, "FullScreen");
        exitFullscreenButton = ResolveButton(exitFullscreenButton, "SmallScreen");

        infoPanel = infoPanel != null ? infoPanel : FindSceneObject("InfoPage", "Info Page", "PaytablePanel");
        guidePanel = guidePanel != null ? guidePanel : FindSceneObject("GuidePage", "Guide Page", "GuidePanel");
        soundPanel = soundPanel != null ? soundPanel : FindSceneObject("SoundPanel", "Sound Panel", "SoundSettingsPanel");

        infoBackButton = ResolveChildButton(infoBackButton, infoPanel, "Back", "InfoBack");
        guideBackButton = ResolveChildButton(guideBackButton, guidePanel, "Back", "GuideBack");
        soundCloseButton = ResolveChildButton(soundCloseButton, soundPanel, "Back", "Close", "SoundBack");
        musicSlider = ResolveChildComponent(musicSlider, soundPanel, "MusicSlider");
        sfxSlider = ResolveChildComponent(sfxSlider, soundPanel, "SfxSlider", "SFXSlider");
        musicToggle = ResolveChildComponent(musicToggle, soundPanel, "MusicToggle");
        sfxToggle = ResolveChildComponent(sfxToggle, soundPanel, "SfxToggle", "SFXToggle");

        auto10Button = ResolveChildButton(null, autoplayPanel, "10");
        auto50Button = ResolveChildButton(null, autoplayPanel, "50");
        auto100Button = ResolveChildButton(null, autoplayPanel, "100");
        auto200Button = ResolveChildButton(null, autoplayPanel, "200");
        auto500Button = ResolveChildButton(null, autoplayPanel, "500");
        autoInfinityButton = ResolveChildButton(null, autoplayPanel, "Infinity");

        if (gameManager == null) Debug.LogError("[UIManager] GameManager was not found; bottom-panel commands cannot run.");
        if (popupManager == null) Debug.LogWarning("[UIManager] PopupManager was not found; popup blocking is unavailable.");
    }

    private void PreparePanels()
    {
        autoplayCanvasGroup = EnsureCanvasGroup(autoplayPanel, autoplayCanvasGroup);
        menuCanvasGroup = EnsureCanvasGroup(menuPanel, menuCanvasGroup);
        soundCanvasGroup = EnsureCanvasGroup(soundPanel, soundCanvasGroup);

        if (autoplayPanel != null)
        {
            RectTransform rect = autoplayPanel.transform as RectTransform;
            if (rect != null)
            {
                autoplayOpenPosition = rect.anchoredPosition;
                autoplayPositionCached = true;
            }
            SetCanvasInteraction(autoplayCanvasGroup, false);
        }

        menuOpen = menuPanel != null && menuPanel.activeSelf;
        soundOpen = soundPanel != null && soundPanel.activeSelf;
        if (freeSpinPanel != null)
        {
            freeSpinPanelOriginalScale = freeSpinPanel.transform.localScale;
            freeSpinPanelScaleCached = true;
            freeSpinPanel.SetActive(false);
        }
        if (freeSpinCountPanel != null) freeSpinCountPanel.SetActive(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        if (freeSpinTotalWinText != null)
        {
            freeSpinTotalWinText.text = FormatSpriteAmount(freeSpinTotalWinText, 0d, 0);
        }
        StopFreeSpinStarFountain();
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(takeFreeSpinButton, false);
        ApplyMenuIcons();
    }

    private void PlayFreeSpinOfferTransition()
    {
        CancelFreeSpinOfferTransition();
        if (freeSpinPanel == null)
        {
            freeSpinStarFountain?.PlayStarBurst();
            return;
        }

        CacheFreeSpinPanelScale();
        EnsureFreeSpinTransitionOverlay();
        if (freeSpinTransitionOverlay == null)
        {
            freeSpinPanel.SetActive(true);
            freeSpinPanel.transform.localScale = Vector3.zero;
            freeSpinStarFountain?.PlayStarBurst();
            freeSpinOfferTransitionTween = freeSpinPanel.transform
                .DOScale(freeSpinPanelOriginalScale, freeSpinOfferScaleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => freeSpinOfferTransitionTween = null);
            return;
        }

        freeSpinPanel.SetActive(false);
        freeSpinPanel.transform.localScale = Vector3.zero;

        freeSpinTransitionOverlay.gameObject.SetActive(true);
        freeSpinTransitionOverlay.transform.SetAsLastSibling();
        freeSpinTransitionOverlay.alpha = 0f;
        freeSpinTransitionOverlay.interactable = true;
        freeSpinTransitionOverlay.blocksRaycasts = true;

        Sequence transition = DOTween.Sequence().SetUpdate(true);
        transition.Append(
            freeSpinTransitionOverlay
                .DOFade(1f, freeSpinFadeToBlackDuration)
                .SetEase(Ease.Linear));
        transition.AppendCallback(() =>
        {
            if (freeSpinPanel == null) return;

            freeSpinPanel.SetActive(true);
            freeSpinPanel.transform.localScale = Vector3.zero;
            freeSpinTransitionOverlay.transform.SetAsLastSibling();
            freeSpinStarFountain?.PlayStarBurst();
        });
        transition.Append(
            freeSpinTransitionOverlay
                .DOFade(0f, freeSpinFadeFromBlackDuration)
                .SetEase(Ease.Linear));
        transition.Join(
            freeSpinPanel.transform
                .DOScale(freeSpinPanelOriginalScale, freeSpinOfferScaleDuration)
                .SetEase(Ease.OutBack));
        transition.OnComplete(() =>
        {
            freeSpinOfferTransitionTween = null;
            freeSpinPanel.transform.localScale = freeSpinPanelOriginalScale;
            HideFreeSpinTransitionOverlay();
            RefreshControls();
        });
        freeSpinOfferTransitionTween = transition;
    }

    private void CacheFreeSpinPanelScale()
    {
        if (freeSpinPanelScaleCached || freeSpinPanel == null)
        {
            return;
        }

        freeSpinPanelOriginalScale = freeSpinPanel.transform.localScale;
        freeSpinPanelScaleCached = true;
    }

    private void EnsureFreeSpinTransitionOverlay()
    {
        if (freeSpinTransitionOverlay != null || freeSpinPanel == null || freeSpinPanel.transform.parent == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            "FreeSpinTransitionBlackOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        overlayObject.layer = freeSpinPanel.layer;

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(freeSpinPanel.transform.parent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = Color.black;
        overlayImage.raycastTarget = true;

        freeSpinTransitionOverlay = overlayObject.GetComponent<CanvasGroup>();
        freeSpinTransitionOverlay.alpha = 0f;
        freeSpinTransitionOverlay.interactable = false;
        freeSpinTransitionOverlay.blocksRaycasts = false;
        overlayObject.SetActive(false);
    }

    private void CancelFreeSpinOfferTransition()
    {
        freeSpinOfferTransitionTween?.Kill();
        freeSpinOfferTransitionTween = null;

        if (freeSpinPanel != null && freeSpinPanelScaleCached)
        {
            freeSpinPanel.transform.localScale = freeSpinPanelOriginalScale;
        }

        HideFreeSpinTransitionOverlay();
    }

    private void HideFreeSpinTransitionOverlay()
    {
        if (freeSpinTransitionOverlay == null)
        {
            return;
        }

        freeSpinTransitionOverlay.alpha = 0f;
        freeSpinTransitionOverlay.interactable = false;
        freeSpinTransitionOverlay.blocksRaycasts = false;
        freeSpinTransitionOverlay.gameObject.SetActive(false);
    }

    private void StopFreeSpinStarFountain()
    {
        if (freeSpinStarFountain == null) return;

        freeSpinStarFountain.StopStarBurst();
        if (freeSpinStarFountain.gameObject.activeSelf)
        {
            freeSpinStarFountain.gameObject.SetActive(false);
        }
    }

    private void ConfigureSpinPointerEvents()
    {
        if (spinButton == null) return;
        spinEventTrigger = spinButton.GetComponent<EventTrigger>();
        if (spinEventTrigger == null) spinEventTrigger = spinButton.gameObject.AddComponent<EventTrigger>();

        spinPointerDownEntry = CreateEventTriggerEntry(EventTriggerType.PointerDown, HandleSpinPointerDown);
        spinPointerUpEntry = CreateEventTriggerEntry(EventTriggerType.PointerUp, HandleSpinPointerUp);
        spinPointerExitEntry = CreateEventTriggerEntry(EventTriggerType.PointerExit, HandleSpinPointerExit);
    }

    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        listenersRegistered = true;
        RegisterSpinPointerEvents();

        Bind(spinButton, HandleSpinClick);
        Bind(stopButton, HandleStopClick);
        Bind(autoplayStopButton, HandleAutoplayStopClick);
        Bind(freeSpinPanelStartButton, HandleFreeSpinStartClick);
        Bind(bottomFreeSpinStartButton, HandleFreeSpinStartClick);
        Bind(takeFreeSpinButton, HandleFreeSpinTakeClick);
        Bind(betIncreaseButton, HandleBetIncrease);
        Bind(betDecreaseButton, HandleBetDecrease);
        Bind(normalSpeedButton, HandleSpeedClick);
        Bind(fastSpeedButton, HandleSpeedClick);
        Bind(skipSpeedButton, HandleSpeedClick);
        Bind(auto10Button, HandleAuto10);
        Bind(auto50Button, HandleAuto50);
        Bind(auto100Button, HandleAuto100);
        Bind(auto200Button, HandleAuto200);
        Bind(auto500Button, HandleAuto500);
        Bind(autoInfinityButton, HandleAutoInfinity);
        Bind(hamburgerButton, OpenMenu);
        Bind(menuCloseButton, CloseMenu);
        Bind(infoButton, OpenInfo);
        Bind(infoBackButton, CloseInfo);
        Bind(guideButton, OpenGuide);
        Bind(guideBackButton, CloseGuide);
        Bind(soundButton, OpenSound);
        Bind(soundCloseButton, CloseSound);
        Bind(homeButton, HandleHome);
        Bind(moreGamesButton, HandleMoreGames);
        Bind(enterFullscreenButton, EnterFullscreen);
        Bind(exitFullscreenButton, ExitFullscreen);

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(HandleMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(HandleSfxVolume);
        if (musicToggle != null) musicToggle.onValueChanged.AddListener(HandleMusicToggle);
        if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(HandleSfxToggle);

        if (gameManager != null)
        {
            gameManager.StateChanged += RefreshControls;
            gameManager.InsufficientBalance += HandleInsufficientBalance;
            gameManager.SpinFailed += HandleSpinFailed;
        }

        if (popupManager != null)
        {
            popupManager.BlockingStateChanged += RefreshControls;
            popupManager.ExitConfirmed += HandleExitConfirmed;
        }
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered) return;
        listenersRegistered = false;
        UnregisterSpinPointerEvents();

        Unbind(spinButton, HandleSpinClick);
        Unbind(stopButton, HandleStopClick);
        Unbind(autoplayStopButton, HandleAutoplayStopClick);
        Unbind(freeSpinPanelStartButton, HandleFreeSpinStartClick);
        Unbind(bottomFreeSpinStartButton, HandleFreeSpinStartClick);
        Unbind(takeFreeSpinButton, HandleFreeSpinTakeClick);
        Unbind(betIncreaseButton, HandleBetIncrease);
        Unbind(betDecreaseButton, HandleBetDecrease);
        Unbind(normalSpeedButton, HandleSpeedClick);
        Unbind(fastSpeedButton, HandleSpeedClick);
        Unbind(skipSpeedButton, HandleSpeedClick);
        Unbind(auto10Button, HandleAuto10);
        Unbind(auto50Button, HandleAuto50);
        Unbind(auto100Button, HandleAuto100);
        Unbind(auto200Button, HandleAuto200);
        Unbind(auto500Button, HandleAuto500);
        Unbind(autoInfinityButton, HandleAutoInfinity);
        Unbind(hamburgerButton, OpenMenu);
        Unbind(menuCloseButton, CloseMenu);
        Unbind(infoButton, OpenInfo);
        Unbind(infoBackButton, CloseInfo);
        Unbind(guideButton, OpenGuide);
        Unbind(guideBackButton, CloseGuide);
        Unbind(soundButton, OpenSound);
        Unbind(soundCloseButton, CloseSound);
        Unbind(homeButton, HandleHome);
        Unbind(moreGamesButton, HandleMoreGames);
        Unbind(enterFullscreenButton, EnterFullscreen);
        Unbind(exitFullscreenButton, ExitFullscreen);

        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(HandleMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSfxVolume);
        if (musicToggle != null) musicToggle.onValueChanged.RemoveListener(HandleMusicToggle);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(HandleSfxToggle);

        if (gameManager != null)
        {
            gameManager.StateChanged -= RefreshControls;
            gameManager.InsufficientBalance -= HandleInsufficientBalance;
            gameManager.SpinFailed -= HandleSpinFailed;
        }

        if (popupManager != null)
        {
            popupManager.BlockingStateChanged -= RefreshControls;
            popupManager.ExitConfirmed -= HandleExitConfirmed;
        }
    }

    private void HandleSpinPointerDown(BaseEventData eventData)
    {
        if (pointerHeld || spinButton == null || !spinButton.interactable || IsBlockingInteraction) return;
        pointerHeld = true;
        longPressTriggered = false;
        suppressNextSpinClick = false;
        if (holdRoutine != null) StopCoroutine(holdRoutine);
        holdRoutine = StartCoroutine(SpinHoldRoutine());
    }

    private void HandleSpinPointerUp(BaseEventData eventData)
    {
        pointerHeld = false;
        StopHoldRoutine();
    }

    private void HandleSpinPointerExit(BaseEventData eventData)
    {
        CancelSpinHold(true);
    }

    private IEnumerator SpinHoldRoutine()
    {
        yield return new WaitForSecondsRealtime(spinHoldDuration);
        holdRoutine = null;

        if (pointerHeld && gameManager != null && gameManager.CanStartManualSpin && !IsBlockingInteraction)
        {
            longPressTriggered = true;
            ShowAutoplayPanel();
        }
    }

    private void HandleSpinClick()
    {
        if (longPressTriggered || suppressNextSpinClick)
        {
            longPressTriggered = false;
            suppressNextSpinClick = false;
            return;
        }

        if (IsBlockingInteraction || gameManager == null) return;
        HideAutoplayPanel();
        if (gameManager.TryStartManualSpin()) audioManager?.PlaySpinClick();
    }

    private void HandleStopClick()
    {
        if (!IsBlockingInteraction && gameManager != null && gameManager.RequestStopSpin())
        {
            audioManager?.PlayNormalClick();
        }
    }

    private void HandleAutoplayStopClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        gameManager.StopAutoSpin();
        audioManager?.PlayNormalClick();
    }

    private void HandleFreeSpinStartClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        if (gameManager.StartPendingFreeSpins()) audioManager?.PlaySpinClick();
    }

    private void HandleFreeSpinTakeClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        if (gameManager.TakeFreeSpinWin()) audioManager?.PlayNormalClick();
    }

    private void HandleBetIncrease() => ChangeBet(true);
    private void HandleBetDecrease() => ChangeBet(false);

    private void ChangeBet(bool increase)
    {
        if (IsBlockingInteraction || gameManager == null || !gameManager.TryChangeBet(increase)) return;
        if (gameManager.IsMaximumBet) audioManager?.PlayMaxBet();
        else audioManager?.PlayNormalClick();
    }

    private void HandleSpeedClick()
    {
        if (IsBlockingInteraction || gameManager == null || !gameManager.CycleSpinSpeed()) return;
        if (gameManager.CurrentSpinSpeed == SpinSpeed.Normal) audioManager?.PlayNormalClick();
        else audioManager?.PlayTurboClick();
    }

    private void HandleAuto10() => SelectAutoplay(10);
    private void HandleAuto50() => SelectAutoplay(50);
    private void HandleAuto100() => SelectAutoplay(100);
    private void HandleAuto200() => SelectAutoplay(200);
    private void HandleAuto500() => SelectAutoplay(500);
    private void HandleAutoInfinity() => SelectAutoplay(InfiniteAutoplay);

    private void SelectAutoplay(int count)
    {
        if (IsBlockingInteraction || gameManager == null) return;
        HideAutoplayPanel();
        if (gameManager.StartAutoSpin(count)) audioManager?.PlaySpinClick();
    }

    private void ShowAutoplayPanel()
    {
        if (autoplayPanel == null || gameManager == null || !gameManager.CanStartManualSpin) return;
        CloseMenu(false);
        CloseInfo();
        CloseGuide();

        RectTransform rect = autoplayPanel.transform as RectTransform;
        if (rect == null) return;
        if (!autoplayPositionCached)
        {
            autoplayOpenPosition = rect.anchoredPosition;
            autoplayPositionCached = true;
        }

        autoplayTween?.Kill();
        autoplayPanel.SetActive(true);
        rect.anchoredPosition = autoplayOpenPosition - Vector2.up * autoplayClosedOffset;
        if (autoplayCanvasGroup != null) autoplayCanvasGroup.alpha = 1f;
        SetCanvasInteraction(autoplayCanvasGroup, true);
        waitForOpeningPointerRelease = true;
        audioManager?.PlayNormalClick();

        autoplayTween = rect.DOAnchorPos(autoplayOpenPosition, autoplaySlideDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                autoplayTween = null;
                RefreshControls();
            });
        RefreshControls();
    }

    private void HideAutoplayPanel()
    {
        if (autoplayPanel == null || !autoplayPanel.activeSelf) return;
        RectTransform rect = autoplayPanel.transform as RectTransform;
        if (rect == null)
        {
            autoplayPanel.SetActive(false);
            return;
        }

        autoplayTween?.Kill();
        SetCanvasInteraction(autoplayCanvasGroup, false);
        autoplayTween = rect.DOAnchorPos(autoplayOpenPosition - Vector2.up * autoplayClosedOffset, autoplaySlideDuration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                autoplayPanel.SetActive(false);
                rect.anchoredPosition = autoplayOpenPosition;
                autoplayTween = null;
                RefreshControls();
            });
        RefreshControls();
    }

    private void OpenMenu()
    {
        if (menuPanel == null || menuOpen || IsBlockingInteraction) return;
        HideAutoplayPanel();
        menuTween?.Kill();
        menuPanel.SetActive(true);
        menuOpen = true;
        if (menuCanvasGroup != null) menuCanvasGroup.alpha = 0f;
        SetCanvasInteraction(menuCanvasGroup, true);
        ApplyMenuIcons();
        audioManager?.PlayNormalClick();
        menuTween = menuCanvasGroup != null
            ? menuCanvasGroup.DOFade(1f, menuFadeDuration).SetUpdate(true).OnComplete(() => { menuTween = null; RefreshControls(); })
            : null;
        RefreshControls();
    }

    private void CloseMenu() => CloseMenu(true);

    private void CloseMenu(bool animate)
    {
        if (menuPanel == null || !menuOpen) return;
        menuTween?.Kill();
        SetCanvasInteraction(menuCanvasGroup, false);
        Action finish = () =>
        {
            menuPanel.SetActive(false);
            menuOpen = false;
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = 0f;
            menuTween = null;
            ApplyMenuIcons();
            RefreshControls();
        };

        if (animate && menuCanvasGroup != null && menuFadeDuration > 0f)
        {
            menuTween = menuCanvasGroup.DOFade(0f, menuFadeDuration).SetUpdate(true).OnComplete(() => finish());
        }
        else
        {
            finish();
        }
        RefreshControls();
    }

    private void ApplyMenuIcons()
    {
        if (hamburgerIcon != null) hamburgerIcon.SetActive(!menuOpen);
        if (downIcon != null) downIcon.SetActive(menuOpen);
    }

    private void OpenInfo()
    {
        if (infoPanel == null)
        {
            Debug.LogWarning("[UIManager] No Info page is assigned; the existing Info button remains a safe hook.");
            return;
        }
        OpenSimplePanel(infoPanel, guidePanel);
    }

    private void CloseInfo()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        RefreshControls();
    }

    private void OpenGuide()
    {
        if (guidePanel == null)
        {
            Debug.LogWarning("[UIManager] No Guide page is assigned; the existing Guide hook is inactive.");
            return;
        }
        OpenSimplePanel(guidePanel, infoPanel);
    }

    private void CloseGuide()
    {
        if (guidePanel != null) guidePanel.SetActive(false);
        RefreshControls();
    }

    private void OpenSimplePanel(GameObject target, GameObject mutuallyExclusive)
    {
        HideAutoplayPanel();
        CloseMenu(false);
        CloseSound();
        if (mutuallyExclusive != null) mutuallyExclusive.SetActive(false);
        target.SetActive(true);
        audioManager?.PlayPopupOpen();
        RefreshControls();
    }

    private void OpenSound()
    {
        if (soundPanel == null)
        {
            Debug.LogWarning("[UIManager] No Sound Settings panel is assigned; persisted audio hooks are ready for its controls.");
            return;
        }

        HideAutoplayPanel();
        CloseMenu(false);
        CloseInfo();
        CloseGuide();
        soundTween?.Kill();
        soundPanel.SetActive(true);
        soundOpen = true;
        soundPanel.transform.localScale = Vector3.zero;
        if (soundCanvasGroup != null) soundCanvasGroup.alpha = 1f;
        SetCanvasInteraction(soundCanvasGroup, true);
        SyncSoundControls();
        audioManager?.PlayPopupOpen();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(soundPanel.transform.DOScale(1.08f, soundTweenDuration * 0.7f).SetEase(Ease.OutCubic));
        sequence.Append(soundPanel.transform.DOScale(1f, soundTweenDuration * 0.3f).SetEase(Ease.InOutSine));
        sequence.OnComplete(() => { soundTween = null; RefreshControls(); });
        soundTween = sequence;
        RefreshControls();
    }

    private void CloseSound()
    {
        if (soundPanel == null || !soundPanel.activeSelf) return;
        soundTween?.Kill();
        SetCanvasInteraction(soundCanvasGroup, false);
        soundTween = soundPanel.transform.DOScale(Vector3.zero, soundTweenDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                soundPanel.SetActive(false);
                soundPanel.transform.localScale = Vector3.one;
                soundOpen = false;
                soundTween = null;
                RefreshControls();
            });
        RefreshControls();
    }

    private void HandleHome()
    {
        if (IsBlockingInteraction || gameManager == null || gameManager.IsAutoplayActive) return;
        CloseMenu(false);
        popupManager?.ShowExitConfirmationPopup();
        RefreshControls();
    }

    private void HandleExitConfirmed()
    {
        gameManager?.ExitGame();
    }

    private void HandleMoreGames()
    {
        if (!moreGamesEnabled || IsBlockingInteraction) return;
        jsBridge?.SendCustomMessage(moreGamesMessage);
        audioManager?.PlayNormalClick();
    }

    private void EnterFullscreen()
    {
        if (IsBlockingInteraction) return;
        jsBridge?.SendCustomMessage(enterFullscreenMessage);
#if !UNITY_WEBGL || UNITY_EDITOR
        Screen.fullScreen = true;
#endif
        fullscreenState = true;
        RefreshControls();
    }

    private void ExitFullscreen()
    {
        if (IsBlockingInteraction) return;
        jsBridge?.SendCustomMessage(exitFullscreenMessage);
#if !UNITY_WEBGL || UNITY_EDITOR
        Screen.fullScreen = false;
#endif
        fullscreenState = false;
        RefreshControls();
    }

    private void HandleInsufficientBalance()
    {
        popupManager?.ShowInsufficientBalancePopup();
        RefreshControls();
    }

    private void HandleSpinFailed(string message)
    {
        popupManager?.ShowServerError(message);
        RefreshControls();
    }

    private void HandleOrientationChanged(OrientationChange.OrientationMode mode, int width, int height)
    {
        CancelSpinHold(true);
        RefreshControls();
    }

    private void HandleMusicVolume(float value) => audioManager?.SetMusicVolume(value);
    private void HandleSfxVolume(float value) => audioManager?.SetSfxVolume(value);
    private void HandleMusicToggle(bool value) => audioManager?.SetMusicEnabled(value);
    private void HandleSfxToggle(bool value) => audioManager?.SetSfxEnabled(value);

    private void SyncSoundControls()
    {
        if (audioManager == null) return;
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(audioManager.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audioManager.SfxVolume);
        if (musicToggle != null) musicToggle.SetIsOnWithoutNotify(audioManager.MusicEnabled);
        if (sfxToggle != null) sfxToggle.SetIsOnWithoutNotify(audioManager.SfxEnabled);
    }

    private bool IsAutoplayDismissalException(Vector2 pointer)
    {
        return IsPointerInside(autoplayPanel, pointer) ||
               IsPointerInside(betIncreaseButton != null ? betIncreaseButton.gameObject : null, pointer) ||
               IsPointerInside(betDecreaseButton != null ? betDecreaseButton.gameObject : null, pointer) ||
               IsPointerInside(normalSpeedButton != null ? normalSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(fastSpeedButton != null ? fastSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(skipSpeedButton != null ? skipSpeedButton.gameObject : null, pointer);
    }

    private static bool IsPointerInside(GameObject target, Vector2 pointer)
    {
        if (target == null || !target.activeInHierarchy) return false;
        RectTransform rect = target.transform as RectTransform;
        if (rect == null) return false;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, camera);
    }

    private static bool TryGetNewPointerDown(out Vector2 screenPosition)
    {
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        screenPosition = default;
        return false;
    }

    private static bool IsAnyPrimaryPointerPressed()
    {
        if (Input.GetMouseButton(0)) return true;
        for (int index = 0; index < Input.touchCount; index++)
        {
            TouchPhase phase = Input.GetTouch(index).phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled) return true;
        }
        return false;
    }

    private void CancelSpinHold(bool suppressClick = false)
    {
        if (suppressClick && pointerHeld) suppressNextSpinClick = true;
        pointerHeld = false;
        longPressTriggered = false;
        StopHoldRoutine();
    }

    private void StopHoldRoutine()
    {
        if (holdRoutine == null) return;
        StopCoroutine(holdRoutine);
        holdRoutine = null;
    }

    private void KillPanelTweens()
    {
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinOfferTransition();
        autoplayTween?.Kill();
        menuTween?.Kill();
        soundTween?.Kill();
        autoplayTween = null;
        menuTween = null;
        soundTween = null;
    }

    private static void SetCanvasInteraction(CanvasGroup group, bool enabled)
    {
        if (group == null) return;
        group.interactable = enabled;
        group.blocksRaycasts = enabled;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target, CanvasGroup assigned)
    {
        if (assigned != null) return assigned;
        if (target == null) return null;
        CanvasGroup existing = target.GetComponent<CanvasGroup>();
        return existing != null ? existing : target.AddComponent<CanvasGroup>();
    }

    private static void SetVisible(Button button, bool visible)
    {
        if (button != null) button.gameObject.SetActive(visible);
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null) selectable.interactable = interactable;
    }

    private static void UpdateAmountText(TMP_Text target, double amount)
    {
        if (target == null) return;
        string formatted = Math.Max(0d, amount).ToString("0.00##");
        bool hasBalancePrefix = target.text.TrimStart().StartsWith("BALANCE", StringComparison.OrdinalIgnoreCase);
        target.text = hasBalancePrefix ? $"BALANCE:  {formatted}" : formatted;
    }

    private static EventTrigger.Entry CreateEventTriggerEntry(
        EventTriggerType eventType,
        UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        return entry;
    }

    private void RegisterSpinPointerEvents()
    {
        if (spinEventTrigger == null) return;
        if (spinEventTrigger.triggers == null) spinEventTrigger.triggers = new List<EventTrigger.Entry>();
        AddTriggerIfMissing(spinEventTrigger, spinPointerDownEntry);
        AddTriggerIfMissing(spinEventTrigger, spinPointerUpEntry);
        AddTriggerIfMissing(spinEventTrigger, spinPointerExitEntry);
    }

    private void UnregisterSpinPointerEvents()
    {
        if (spinEventTrigger == null || spinEventTrigger.triggers == null) return;
        spinEventTrigger.triggers.Remove(spinPointerDownEntry);
        spinEventTrigger.triggers.Remove(spinPointerUpEntry);
        spinEventTrigger.triggers.Remove(spinPointerExitEntry);
    }

    private static void AddTriggerIfMissing(EventTrigger trigger, EventTrigger.Entry entry)
    {
        if (trigger != null && entry != null && !trigger.triggers.Contains(entry))
        {
            trigger.triggers.Add(entry);
        }
    }

    private static void Bind(Button button, UnityAction listener)
    {
        if (button != null) button.onClick.AddListener(listener);
    }

    private static void Unbind(Button button, UnityAction listener)
    {
        if (button != null) button.onClick.RemoveListener(listener);
    }

    private static Button ResolveButton(Button assigned, params string[] names)
    {
        return assigned != null ? assigned : FindNamedComponent<Button>(names);
    }

    private static Button ResolveChildButton(Button assigned, GameObject root, params string[] names)
    {
        return ResolveChildComponent(assigned, root, names);
    }

    private Button FindBottomFreeSpinStartButton()
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.name == "Start" &&
                (freeSpinPanel == null || !candidate.transform.IsChildOf(freeSpinPanel.transform)));
    }

    private static T ResolveChildComponent<T>(T assigned, GameObject root, params string[] names) where T : Component
    {
        if (assigned != null) return assigned;
        if (root == null) return null;
        return root.GetComponentsInChildren<T>(true).FirstOrDefault(candidate => names.Contains(candidate.name));
    }

    private static T FindNamedComponent<T>(params string[] names) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene.IsValid() && names.Contains(candidate.name));
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene.IsValid());
    }

    private static GameObject FindSceneObject(params string[] names)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => names.Contains(candidate.name));
    }
}
