using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
    private const float MinimumFreeSpinOfferFadeDuration = 2f;
    private const float FullCircleRadians = Mathf.PI * 2f;

    [Header("Controllers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private JSFunctCalls jsBridge;
    [SerializeField] private AudioManager audioManager;

    [Header("Portrait UI")]
    [SerializeField] private GameObject portraitUiRoot;

    [Header("Portrait Jackpot Animation")]
    [SerializeField, Min(0f)] private float portraitJackpotBobDistance = 10f;
    [SerializeField, Min(0.01f)] private float portraitJackpotBobCycleDuration = 4f;

    [Header("Game Values")]
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text betAmountText;

    [Header("Jackpot Values")]
    [SerializeField] private TMP_Text miniJackpotText;
    [SerializeField] private TMP_Text minorJackpotText;
    [SerializeField] private TMP_Text majorJackpotText;
    [SerializeField] private TMP_Text grandJackpotText;
    [SerializeField] private TMP_Text portraitMiniJackpotText;
    [SerializeField] private TMP_Text portraitMinorJackpotText;
    [SerializeField] private TMP_Text portraitMajorJackpotText;
    [SerializeField] private TMP_Text portraitGrandJackpotText;

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
    [SerializeField] private GameObject portraitFreeSpinCountPanel;
    [SerializeField] private GameObject freeSpinWinPanel;
    [SerializeField] private Button freeSpinPanelStartButton;
    [SerializeField] private Button bottomFreeSpinStartButton;
    [SerializeField] private Button collectFreeSpinButton;
    [SerializeField] private Button landscapeTakeFreeSpinButton;
    [SerializeField] private Button portraitTakeFreeSpinButton;
    [SerializeField] private TMP_Text totalFreeSpinsText;
    [SerializeField] private TMP_Text remainingFreeSpinsText;
    [SerializeField] private TMP_Text portraitTotalFreeSpinsText;
    [SerializeField] private TMP_Text portraitRemainingFreeSpinsText;
    [SerializeField] private TMP_Text freeSpinTotalWinText;
    [SerializeField] private StarFountain freeSpinStarFountain;
    [SerializeField, Min(0f)] private float freeSpinTotalWinCountDuration = 1.5f;

    [Header("Extra Win Presentation")]
    [SerializeField] private GameObject extraWinPanel;
    [SerializeField] private Image extraWinTitleImage;
    [SerializeField] private Sprite bigWinSprite;
    [SerializeField] private Sprite superBigWinSprite;
    [SerializeField] private Vector2 extraWinLandscapeSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 extraWinPortraitSize = new Vector2(2000f, 3500f);
    [SerializeField] private Vector2 bigWinTitleSize = new Vector2(700f, 700f);
    [SerializeField] private Vector2 superBigWinTitleSize = new Vector2(1000f, 1000f);
    [SerializeField] private TMP_Text extraWinAmountText;
    [SerializeField] private StarFountain extraWinCoinFountain;
    [SerializeField, Min(1f)] private float extraWinOvershootScale = 1.12f;
    [SerializeField, Min(0f)] private float extraWinGrowDuration = 0.3f;
    [SerializeField, Min(0f)] private float extraWinSettleDuration = 0.15f;
    [SerializeField, Min(4f)] private float extraWinCountDuration = 4f;
    [SerializeField, Min(0f)] private float extraWinFinalAmountHoldDuration = 0.5f;
    [SerializeField, Min(0f)] private float extraWinExitDuration = 0.2f;

    [Header("Free Spin Offer Transition")]
    [SerializeField] private CanvasGroup freeSpinTransitionOverlay;
    [SerializeField] private Transform freeSpinScaleTarget;
    [SerializeField, Min(MinimumFreeSpinOfferFadeDuration)] private float freeSpinFadeToBlackDuration = 2f;
    [SerializeField, Min(1f)] private float freeSpinBlackHoldDuration = 1f;
    [SerializeField, Min(MinimumFreeSpinOfferFadeDuration)] private float freeSpinFadeFromBlackDuration = 2f;
    [SerializeField, Min(0f)] private float freeSpinOfferScaleDuration = 0.5f;

    [Header("Free Spin Completion Transition")]
    [SerializeField] private CanvasGroup freeSpinWinCanvasGroup;
    [SerializeField, Min(0f)] private float freeSpinWinFadeOutDuration = 1f;
    [SerializeField, Min(0f)] private float freeSpinGameplayRevealDuration = 0.25f;

    [Header("Autoplay Panel")]
    [SerializeField] private GameObject autoplayPanel;
    [SerializeField] private CanvasGroup autoplayCanvasGroup;
    [SerializeField, Min(0.1f)] private float spinHoldDuration = 0.75f;
    [SerializeField, Min(0f)] private float autoplaySlideDuration = 0.6075f;
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
    [SerializeField] private Button portraitEnterFullscreenButton;
    [SerializeField] private Button portraitExitFullscreenButton;
    [SerializeField] private bool moreGamesEnabled = true;
    [SerializeField] private string moreGamesMessage = "more_games";

    private TMP_Text portraitBalanceText;
    private TMP_Text portraitBetAmountText;
    private Button portraitSpinButton;
    private Button portraitStopButton;
    private Button portraitAutoplayStopButton;
    private TMP_Text portraitAutoplayCountText;
    private Button portraitBetIncreaseButton;
    private Button portraitBetDecreaseButton;
    private Button portraitNormalSpeedButton;
    private Button portraitFastSpeedButton;
    private Button portraitSkipSpeedButton;
    private Button portraitBottomFreeSpinStartButton;
    private GameObject portraitAutoplayPanel;
    private CanvasGroup portraitAutoplayCanvasGroup;
    private Button portraitAuto10Button;
    private Button portraitAuto50Button;
    private Button portraitAuto100Button;
    private Button portraitAuto200Button;
    private Button portraitAuto500Button;
    private Button portraitAutoInfinityButton;
    private Button portraitHamburgerButton;
    private Button portraitMenuCloseButton;
    private GameObject portraitHamburgerIcon;
    private GameObject portraitDownIcon;
    private GameObject portraitMenuPanel;
    private CanvasGroup portraitMenuCanvasGroup;
    private Button portraitInfoButton;
    private Button portraitGuideButton;
    private Button portraitSoundButton;
    private Button portraitHomeButton;
    private Button portraitMoreGamesButton;
    private RectTransform portraitJackpotTopPanel;

    private readonly List<EventTrigger> spinEventTriggers = new List<EventTrigger>();
    private readonly List<RectTransform> portraitJackpotTargets = new List<RectTransform>();
    private readonly List<Vector2> portraitJackpotStartingPositions = new List<Vector2>();
    private EventTrigger.Entry spinPointerDownEntry;
    private EventTrigger.Entry spinPointerUpEntry;
    private EventTrigger.Entry spinPointerExitEntry;
    private Coroutine holdRoutine;
    private Tween autoplayTween;
    private Tween portraitAutoplayTween;
    private Tween menuTween;
    private Tween portraitMenuTween;
    private Tween soundTween;
    private Tween freeSpinOfferTransitionTween;
    private Tween freeSpinCollectTransitionTween;
    private Tween freeSpinTotalWinTween;
    private Tween portraitJackpotBobTween;
    private Tween extraWinTween;
    private Vector3 freeSpinScaleTargetOriginalScale = Vector3.one;
    private bool freeSpinScaleTargetScaleCached;
    private RectTransform autoplayViewport;
    private RectTransform portraitAutoplayViewport;
    private Vector3 autoplayRestingPosition;
    private bool autoplayRestingPositionCached;
    private Vector3 portraitAutoplayRestingPosition;
    private bool portraitAutoplayRestingPositionCached;
    private bool pointerHeld;
    private bool longPressTriggered;
    private bool suppressNextSpinClick;
    private bool waitForOpeningPointerRelease;
    private bool menuOpen;
    private bool soundOpen;
    private bool listenersRegistered;
    private bool fullscreenListenerRegistered;
    private bool fullscreenState;
    private bool isMobilePortrait;
    private bool lastSocketConnected;
    private bool lastPopupBlockingState;
    private JackpotValues currentJackpotValues;
    private GameConfig appliedInfoConfig;
    private bool infoPageValuesApplied;
    private bool extraWinPresentationActive;
    private bool extraWinOriginalScaleCached;
    private Vector3 extraWinOriginalScale = Vector3.one;
    private Action extraWinCompletion;

    // Autoplay and hamburger panels are non-modal: their animations must not
    // disable the surrounding game controls. Sound and server/error popups
    // remain modal and continue to block background input.
    private bool IsBlockingInteraction =>
        soundOpen || extraWinPresentationActive ||
        (popupManager != null && popupManager.IsBlockingPopupActive);

    private void Awake()
    {
        ResolveReferences();
        InitializeExtraWinOrientation();
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
        RegisterFullscreenListener();
        OrientationChange.OnOrientationChanged += HandleOrientationChanged;
        UpdatePortraitJackpotBobState();
        RefreshControls();
    }

    private void Start()
    {
        lastSocketConnected = gameManager != null && gameManager.IsSocketConnected;
        lastPopupBlockingState = popupManager != null && popupManager.IsBlockingPopupActive;
        SyncSoundControls();
        ApplyInfoPageValues(gameManager != null && gameManager.IsInitialized
            ? gameManager.GameConfig
            : null);
        RefreshControls();
    }

    private void OnDisable()
    {
        UnregisterFullscreenListener();
        UnregisterListeners();
        OrientationChange.OnOrientationChanged -= HandleOrientationChanged;
        CancelSpinHold();
        KillPanelTweens();
    }

    private void Update()
    {
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

        if (IsAnyAutoplayPanelOpen() && !IsAutoplayDismissalException(screenPosition))
        {
            HideAutoplayPanel();
        }

        if (menuOpen &&
            !IsPointerInside(menuPanel, screenPosition) &&
            !IsPointerInside(portraitMenuPanel, screenPosition) &&
            !IsPointerInside(hamburgerButton != null ? hamburgerButton.gameObject : null, screenPosition) &&
            !IsPointerInside(portraitHamburgerButton != null ? portraitHamburgerButton.gameObject : null, screenPosition) &&
            !IsPointerInside(menuCloseButton != null ? menuCloseButton.gameObject : null, screenPosition) &&
            !IsPointerInside(portraitMenuCloseButton != null ? portraitMenuCloseButton.gameObject : null, screenPosition))
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
        foreach (TMP_Text pingText in FindNamedComponents<TMP_Text>("PingText"))
        {
            pingText.text = value;
        }
    }

    internal void UpdatePingDisplay(int milliseconds)
    {
        UpdatePingDisplay($"{milliseconds} ms");
    }

    internal void UpdateJackpotDisplay(JackpotValues values)
    {
        if (values == null)
        {
            Debug.LogWarning("[UIManager] Ignored a null jackpot snapshot.");
            return;
        }

        // Copy the full server snapshot so later DTO mutation cannot produce a
        // partially updated display.
        currentJackpotValues = new JackpotValues
        {
            miniJackpot = values.miniJackpot,
            minorJackpot = values.minorJackpot,
            majorJackpot = values.majorJackpot,
            grandJackpot = values.grandJackpot
        };

        RefreshJackpotTexts();
    }

    private void RefreshJackpotTexts()
    {
        JackpotValues values = currentJackpotValues ?? gameManager?.GameConfig?.jackpotData?.values;
        if (values == null)
        {
            return;
        }

        string mini = FormatJackpotValue(values.miniJackpot);
        string minor = FormatJackpotValue(values.minorJackpot);
        string major = FormatJackpotValue(values.majorJackpot);
        string grand = FormatJackpotValue(values.grandJackpot);

        SetJackpotText(miniJackpotText, mini);
        SetJackpotText(minorJackpotText, minor);
        SetJackpotText(majorJackpotText, major);
        SetJackpotText(grandJackpotText, grand);
        SetJackpotText(portraitMiniJackpotText, mini);
        SetJackpotText(portraitMinorJackpotText, minor);
        SetJackpotText(portraitMajorJackpotText, major);
        SetJackpotText(portraitGrandJackpotText, grand);
    }

    private static string FormatJackpotValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal amount))
        {
            return "$" + amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        return "$" + value.Trim();
    }

    private static void SetJackpotText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    internal void UpdateBalanceDisplay()
    {
        RefreshControls();
    }

    internal bool ShowExtraWinPresentation(
        WinPopupType popupType,
        double winAmount,
        int decimalPlaces,
        Action onComplete)
    {
        if (popupType != WinPopupType.BigWin && popupType != WinPopupType.SuperBigWin)
        {
            return false;
        }

        if (extraWinPanel == null)
        {
            Debug.LogWarning("[UIManager] ExtraWin was not found; the win popup cannot be shown.");
            return false;
        }

        ResetExtraWinPresentation(false);
        CacheExtraWinOriginalScale();
        extraWinCompletion = onComplete;
        extraWinPresentationActive = true;

        Sprite selectedSprite = popupType == WinPopupType.SuperBigWin
            ? superBigWinSprite
            : bigWinSprite;
        if (extraWinTitleImage != null && selectedSprite != null)
        {
            extraWinTitleImage.sprite = selectedSprite;
        }

        if (extraWinTitleImage != null)
        {
            extraWinTitleImage.rectTransform.sizeDelta = popupType == WinPopupType.SuperBigWin
                ? superBigWinTitleSize
                : bigWinTitleSize;
        }

        int safeDecimalPlaces = decimalPlaces >= 0
            ? Mathf.Clamp(decimalPlaces, 0, 28)
            : 2;
        double finalWinAmount = Math.Max(0d, winAmount);
        if (extraWinAmountText != null)
        {
            extraWinAmountText.text = FormatSpriteAmount(
                extraWinAmountText,
                0d,
                safeDecimalPlaces);
        }

        extraWinPanel.SetActive(true);
        extraWinPanel.transform.SetAsLastSibling();
        extraWinPanel.transform.localScale = Vector3.zero;
        audioManager?.PlayExtraWin(popupType);

        float countDuration = Mathf.Max(4f, extraWinCountDuration);
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(
            extraWinPanel.transform
                .DOScale(extraWinOriginalScale * extraWinOvershootScale, extraWinGrowDuration)
                .SetEase(Ease.OutCubic));
        sequence.Append(
            extraWinPanel.transform
                .DOScale(extraWinOriginalScale, extraWinSettleDuration)
                .SetEase(Ease.InOutSine));
        sequence.AppendCallback(() =>
        {
            extraWinCoinFountain?.PlayCenterCoinShower(
                countDuration + extraWinFinalAmountHoldDuration);
        });

        double displayedAmount = 0d;
        Tween amountTween = DOTween.To(
                () => displayedAmount,
                value =>
                {
                    displayedAmount = value;
                    if (extraWinAmountText != null)
                    {
                        extraWinAmountText.text = FormatSpriteAmount(
                            extraWinAmountText,
                            displayedAmount,
                            safeDecimalPlaces);
                    }
                },
                finalWinAmount,
                countDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                if (extraWinAmountText != null)
                {
                    extraWinAmountText.text = FormatSpriteAmount(
                        extraWinAmountText,
                        finalWinAmount,
                        safeDecimalPlaces);
                }
            });
        sequence.Append(amountTween);
        sequence.AppendInterval(extraWinFinalAmountHoldDuration);
        sequence.Append(
            extraWinPanel.transform
                .DOScale(Vector3.zero, extraWinExitDuration)
                .SetEase(Ease.InBack));
        sequence.OnComplete(CompleteExtraWinPresentation);
        extraWinTween = sequence;
        RefreshControls();
        return true;
    }

    internal void HideExtraWinPresentation()
    {
        ResetExtraWinPresentation(true);
        RefreshControls();
    }

    private void CompleteExtraWinPresentation()
    {
        Action completion = extraWinCompletion;
        ResetExtraWinPresentation(true);
        RefreshControls();
        completion?.Invoke();
    }

    private void ResetExtraWinPresentation(bool clearCompletion)
    {
        if (extraWinTween != null && extraWinTween.IsActive())
        {
            extraWinTween.Kill();
        }

        extraWinTween = null;
        extraWinCoinFountain?.StopStarBurst();
        extraWinPresentationActive = false;
        if (extraWinPanel != null)
        {
            CacheExtraWinOriginalScale();
            extraWinPanel.transform.localScale = extraWinOriginalScale;
            extraWinPanel.SetActive(false);
        }

        if (clearCompletion)
        {
            extraWinCompletion = null;
        }
    }

    private void CacheExtraWinOriginalScale()
    {
        if (extraWinOriginalScaleCached || extraWinPanel == null)
        {
            return;
        }

        extraWinOriginalScale = extraWinPanel.transform.localScale;
        if (extraWinOriginalScale == Vector3.zero)
        {
            extraWinOriginalScale = Vector3.one;
        }

        extraWinOriginalScaleCached = true;
    }

    internal void ShowFreeSpinOffer(int totalSpins, int remainingSpins)
    {
        // The black-screen offer transition belongs only to the server-confirmed
        // Free Games trigger state set by GameManager. Ignore every other caller.
        if (gameManager == null || !gameManager.IsFreeSpinAwaitingStart)
        {
            return;
        }

        CancelFreeSpinTotalWinCount();
        StopFreeSpinStarFountain();
        SetFreeSpinCountPanelVisible(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        SetVisible(bottomFreeSpinStartButton, true);
        SetVisible(portraitBottomFreeSpinStartButton, true);
        SetFreeSpinTakeButtonsVisible(false);
        UpdateFreeSpinCounter(totalSpins, remainingSpins);
        PlayFreeSpinOfferTransition();
        RefreshControls();
    }

    internal void BeginFreeSpinPresentation(
        int totalSpins,
        int remainingSpins,
        Action onPanelHidden)
    {
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        SetFreeSpinCountPanelVisible(true);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(portraitBottomFreeSpinStartButton, false);
        SetFreeSpinTakeButtonsVisible(false);
        UpdateFreeSpinCounter(totalSpins, remainingSpins);

        Action finishPanelExit = () =>
        {
            freeSpinOfferTransitionTween = null;
            if (freeSpinPanel != null)
            {
                freeSpinPanel.SetActive(false);
                freeSpinPanel.transform.localScale = Vector3.one;
            }

            if (freeSpinScaleTarget != null)
            {
                freeSpinScaleTarget.localScale = freeSpinScaleTargetOriginalScale;
            }

            SetFreeSpinCountPanelVisible(true);
            RefreshControls();
            onPanelHidden?.Invoke();
        };

        if (freeSpinPanel == null || !freeSpinPanel.activeSelf ||
            freeSpinScaleTarget == null || freeSpinOfferScaleDuration <= 0f)
        {
            finishPanelExit();
            return;
        }

        CacheFreeSpinScaleTarget();
        freeSpinPanel.transform.localScale = Vector3.one;
        freeSpinScaleTarget.localScale = freeSpinScaleTargetOriginalScale;
        freeSpinOfferTransitionTween = freeSpinScaleTarget
            .DOScale(Vector3.zero, freeSpinOfferScaleDuration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .OnComplete(() => finishPanelExit());
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

        if (portraitTotalFreeSpinsText != null)
        {
            portraitTotalFreeSpinsText.text = FormatSpriteInteger(portraitTotalFreeSpinsText, totalSpins);
        }

        if (portraitRemainingFreeSpinsText != null)
        {
            portraitRemainingFreeSpinsText.text = FormatSpriteInteger(portraitRemainingFreeSpinsText, remainingSpins);
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
        CancelFreeSpinCollectTransition();
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
        SetFreeSpinCountPanelVisible(false);
        if (freeSpinWinPanel != null)
        {
            freeSpinWinPanel.SetActive(true);
            freeSpinWinCanvasGroup = EnsureCanvasGroup(freeSpinWinPanel, freeSpinWinCanvasGroup);
        }
        if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
        SetCanvasInteraction(freeSpinWinCanvasGroup, true);
        StartFreeSpinTotalWinCount(formattedTotalWin);
        freeSpinStarFountain?.PlayStarBurst();
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(portraitBottomFreeSpinStartButton, false);
        SetFreeSpinTakeButtonsVisible(true);
        RefreshControls();
    }

    internal bool PlayFreeSpinCollectTransition(Action onBlackout)
    {
        if (freeSpinWinPanel == null || !freeSpinWinPanel.activeSelf)
        {
            return false;
        }

        CancelFreeSpinCollectTransition();
        if (freeSpinTotalWinTween != null)
        {
            freeSpinTotalWinTween.Complete();
            freeSpinTotalWinTween = null;
        }

        StopFreeSpinStarFountain();
        freeSpinWinCanvasGroup = EnsureCanvasGroup(freeSpinWinPanel, freeSpinWinCanvasGroup);
        if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
        SetCanvasInteraction(freeSpinWinCanvasGroup, false);
        SetFreeSpinTakeButtonsVisible(false);

        EnsureFreeSpinTransitionOverlay();
        CanvasGroup transitionOverlay = freeSpinTransitionOverlay;
        if (transitionOverlay != null)
        {
            transitionOverlay.gameObject.SetActive(true);
            transitionOverlay.transform.SetAsLastSibling();
            transitionOverlay.alpha = 0f;
            transitionOverlay.interactable = true;
            transitionOverlay.blocksRaycasts = true;
        }

        Sequence transition = DOTween.Sequence().SetUpdate(true);
        if (freeSpinWinCanvasGroup != null)
        {
            transition.Append(
                freeSpinWinCanvasGroup
                    .DOFade(0f, freeSpinWinFadeOutDuration)
                    .SetEase(Ease.InOutSine));
        }

        transition.AppendCallback(() =>
        {
            if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
            if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
            SetFreeSpinCountPanelVisible(true);
            RefreshControls();
        });

        if (freeSpinGameplayRevealDuration > 0f)
        {
            transition.AppendInterval(freeSpinGameplayRevealDuration);
        }

        if (transitionOverlay != null)
        {
            transition.AppendCallback(() =>
            {
                transitionOverlay.transform.SetAsLastSibling();
            });
            transition.Append(
                transitionOverlay
                    .DOFade(1f, freeSpinFadeToBlackDuration)
                    .SetEase(Ease.Linear));
            transition.AppendInterval(Mathf.Max(1f, freeSpinBlackHoldDuration));
        }

        transition.AppendCallback(() =>
        {
            audioManager?.PlayBackgroundMusic();
            onBlackout?.Invoke();
            if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
            SetFreeSpinCountPanelVisible(false);
            if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
            if (transitionOverlay != null) transitionOverlay.transform.SetAsLastSibling();
        });

        if (transitionOverlay != null)
        {
            transition.Append(
                transitionOverlay
                    .DOFade(0f, freeSpinFadeFromBlackDuration)
                    .SetEase(Ease.Linear));
        }

        transition.OnComplete(() =>
        {
            freeSpinCollectTransitionTween = null;
            if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
            SetCanvasInteraction(freeSpinWinCanvasGroup, false);
            HideFreeSpinTransitionOverlay();
            StopFreeSpinStarFountain();
            RefreshControls();
        });
        freeSpinCollectTransitionTween = transition;
        RefreshControls();
        return true;
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
        audioManager?.PlayBackgroundMusic();
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinCollectTransition();
        CancelFreeSpinOfferTransition();
        StopFreeSpinStarFountain();
        if (freeSpinPanel != null) freeSpinPanel.SetActive(false);
        SetFreeSpinCountPanelVisible(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
        SetCanvasInteraction(freeSpinWinCanvasGroup, false);
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(portraitBottomFreeSpinStartButton, false);
        SetFreeSpinTakeButtonsVisible(false);
        RefreshControls();
    }

    /// <summary>Called by the browser fullscreen listener with "1" or "0".</summary>
    public void OnFullscreenChanged(string value)
    {
        if (value == "1")
        {
            fullscreenState = true;
        }
        else if (value == "0")
        {
            fullscreenState = false;
        }
        else
        {
            Debug.LogWarning($"[Fullscreen] Ignored invalid browser state '{value}'. Expected '1' or '0'.");
            return;
        }

        ApplyFullscreenButtonState();
    }

    private void RegisterFullscreenListener()
    {
        ApplyFullscreenButtonState();
        if (jsBridge == null)
        {
            Debug.LogWarning("[Fullscreen] JSFunctCalls is missing. Fullscreen controls will remain windowed.");
            fullscreenState = false;
            ApplyFullscreenButtonState();
            return;
        }

        jsBridge.RegisterFullscreenListener(gameObject.name);
        fullscreenListenerRegistered = true;
    }

    private void UnregisterFullscreenListener()
    {
        if (!fullscreenListenerRegistered || jsBridge == null) return;
        jsBridge.UnregisterFullscreenListener();
        fullscreenListenerRegistered = false;
    }

    private void ApplyFullscreenButtonState()
    {
        if (enterFullscreenButton != null) enterFullscreenButton.gameObject.SetActive(!fullscreenState);
        if (portraitEnterFullscreenButton != null) portraitEnterFullscreenButton.gameObject.SetActive(!fullscreenState);
        if (exitFullscreenButton != null) exitFullscreenButton.gameObject.SetActive(fullscreenState);
        if (portraitExitFullscreenButton != null) portraitExitFullscreenButton.gameObject.SetActive(fullscreenState);
    }

    internal void RefreshControls()
    {
        RefreshJackpotTexts();
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
        bool showSpin = !freeSpinOffer && !freeSpinAwaitingTake && !showStop && !showAutoplayStop;

        SetVisible(spinButton, showSpin);
        SetVisible(portraitSpinButton, showSpin);
        SetVisible(stopButton, showStop);
        SetVisible(portraitStopButton, showStop);
        SetVisible(autoplayStopButton, showAutoplayStop);
        SetVisible(portraitAutoplayStopButton, showAutoplayStop);
        SetVisible(bottomFreeSpinStartButton, freeSpinOffer);
        SetVisible(portraitBottomFreeSpinStartButton, freeSpinOffer);
        SetFreeSpinTakeButtonsVisible(freeSpinAwaitingTake);

        bool spinInteractable = showSpin && !extraGiftWildReveal && !freeSpinActive && !blocked &&
            !settlingAutoplay && !showingResult && gameManager.CanAttemptManualSpin;
        SetInteractable(spinButton, spinInteractable);
        SetInteractable(portraitSpinButton, spinInteractable);

        bool stopInteractable = showStop && !blocked && !gameManager.IsStopRequested;
        SetInteractable(stopButton, stopInteractable);
        SetInteractable(portraitStopButton, stopInteractable);

        bool autoplayStopInteractable = showAutoplayStop && !blocked;
        SetInteractable(autoplayStopButton, autoplayStopInteractable);
        SetInteractable(portraitAutoplayStopButton, autoplayStopInteractable);

        bool freeSpinStartInteractable = freeSpinOffer && !blocked;
        SetInteractable(bottomFreeSpinStartButton, freeSpinStartInteractable);
        SetInteractable(portraitBottomFreeSpinStartButton, freeSpinStartInteractable);

        if (freeSpinPanelStartButton != null)
        {
            freeSpinPanelStartButton.interactable = freeSpinOffer && !blocked;
        }

        SetFreeSpinTakeButtonsInteractable(freeSpinAwaitingTake && !blocked);

        string autoplayCount = gameManager.AutoplaySpinsRemaining < 0
            ? "\u221E"
            : gameManager.AutoplaySpinsRemaining.ToString();
        UpdateAutoplayCount(autoplayCountText, autoplay, autoplayCount);
        UpdateAutoplayCount(portraitAutoplayCountText, autoplay, autoplayCount);

        UpdateAmountText(balanceText, gameManager.CurrentBalance);
        UpdateAmountText(portraitBalanceText, gameManager.CurrentBalance);
        UpdateAmountText(betAmountText, gameManager.CurrentTotalBet);
        UpdateAmountText(portraitBetAmountText, gameManager.CurrentTotalBet);

        bool allowBetChange = !blocked && gameManager.CanChangeBet;
        SetInteractable(betIncreaseButton, allowBetChange);
        SetInteractable(portraitBetIncreaseButton, allowBetChange);
        SetInteractable(betDecreaseButton, allowBetChange);
        SetInteractable(portraitBetDecreaseButton, allowBetChange);

        SpinSpeed speed = gameManager.CurrentSpinSpeed;
        bool allowSpeedChange = !blocked && gameManager.CanChangeSpinSpeed;
        SetVisible(normalSpeedButton, speed == SpinSpeed.Normal);
        SetVisible(portraitNormalSpeedButton, speed == SpinSpeed.Normal);
        SetVisible(fastSpeedButton, speed == SpinSpeed.Turbo);
        SetVisible(portraitFastSpeedButton, speed == SpinSpeed.Turbo);
        SetVisible(skipSpeedButton, speed == SpinSpeed.QuickSpin);
        SetVisible(portraitSkipSpeedButton, speed == SpinSpeed.QuickSpin);
        SetInteractable(normalSpeedButton, allowSpeedChange);
        SetInteractable(portraitNormalSpeedButton, allowSpeedChange);
        SetInteractable(fastSpeedButton, allowSpeedChange);
        SetInteractable(portraitFastSpeedButton, allowSpeedChange);
        SetInteractable(skipSpeedButton, allowSpeedChange);
        SetInteractable(portraitSkipSpeedButton, allowSpeedChange);

        SetInteractable(hamburgerButton, !blocked && !menuOpen);
        SetInteractable(portraitHamburgerButton, !blocked && !menuOpen);
        SetInteractable(menuCloseButton, !blocked && menuOpen);
        SetInteractable(portraitMenuCloseButton, !blocked && menuOpen);
        bool allowExit = !blocked && !autoplay && !settlingAutoplay && !freeSpinOffer && !freeSpinActive;
        SetInteractable(homeButton, allowExit);
        SetInteractable(portraitHomeButton, allowExit);
        SetInteractable(moreGamesButton, false);
        SetInteractable(portraitMoreGamesButton, false);
        SetInteractable(enterFullscreenButton, !blocked);
        SetInteractable(portraitEnterFullscreenButton, !blocked);
        SetInteractable(exitFullscreenButton, !blocked);
        SetInteractable(portraitExitFullscreenButton, !blocked);

        ApplyFullscreenButtonState();

        bool autoplayChoicesEnabled = !blocked && gameManager.CanStartManualSpin;
        SetInteractable(auto10Button, autoplayChoicesEnabled);
        SetInteractable(portraitAuto10Button, autoplayChoicesEnabled);
        SetInteractable(auto50Button, autoplayChoicesEnabled);
        SetInteractable(portraitAuto50Button, autoplayChoicesEnabled);
        SetInteractable(auto100Button, autoplayChoicesEnabled);
        SetInteractable(portraitAuto100Button, autoplayChoicesEnabled);
        SetInteractable(auto200Button, autoplayChoicesEnabled);
        SetInteractable(portraitAuto200Button, autoplayChoicesEnabled);
        SetInteractable(auto500Button, autoplayChoicesEnabled);
        SetInteractable(portraitAuto500Button, autoplayChoicesEnabled);
        SetInteractable(autoInfinityButton, autoplayChoicesEnabled);
        SetInteractable(portraitAutoInfinityButton, autoplayChoicesEnabled);
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
        miniJackpotText = miniJackpotText != null ? miniJackpotText : FindNamedComponent<TMP_Text>("MiniText");
        minorJackpotText = minorJackpotText != null ? minorJackpotText : FindNamedComponent<TMP_Text>("MinorText");
        majorJackpotText = majorJackpotText != null ? majorJackpotText : FindNamedComponent<TMP_Text>("MajorText");
        grandJackpotText = grandJackpotText != null ? grandJackpotText : FindNamedComponent<TMP_Text>("GrandText");

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
        portraitFreeSpinCountPanel = portraitFreeSpinCountPanel != null
            ? portraitFreeSpinCountPanel
            : FindSceneObject("FreeSpinCountPanelPortrait");
        freeSpinWinPanel = freeSpinWinPanel != null ? freeSpinWinPanel : FindSceneObject("FreeSpinWinPanel");
        freeSpinTransitionOverlay = freeSpinTransitionOverlay != null
            ? freeSpinTransitionOverlay
            : FindNamedComponent<CanvasGroup>("BlackScreen");
        freeSpinScaleTarget = freeSpinScaleTarget != null || freeSpinPanel == null
            ? freeSpinScaleTarget
            : freeSpinPanel
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "CanScale");
        freeSpinPanelStartButton = ResolveChildButton(freeSpinPanelStartButton, freeSpinPanel, "Start");
        bottomFreeSpinStartButton = bottomFreeSpinStartButton != null
            ? bottomFreeSpinStartButton
            : FindBottomFreeSpinStartButton();
        collectFreeSpinButton = ResolveChildButton(
            collectFreeSpinButton,
            freeSpinWinPanel,
            "Collect",
            "COLLECT");
        GameObject landscapeBottomPanel = spinButton != null && spinButton.transform.parent != null
            ? spinButton.transform.parent.gameObject
            : null;
        landscapeTakeFreeSpinButton = ResolveChildButton(
            landscapeTakeFreeSpinButton,
            landscapeBottomPanel,
            "Take",
            "TAKE");
        totalFreeSpinsText = ResolveChildComponent(totalFreeSpinsText, freeSpinCountPanel, "TotalFreeSpins");
        remainingFreeSpinsText = ResolveChildComponent(
            remainingFreeSpinsText,
            freeSpinCountPanel,
            "RemainingFreeSpins",
            "RemainingFreeSpin");
        portraitTotalFreeSpinsText = ResolveChildComponent(
            portraitTotalFreeSpinsText,
            portraitFreeSpinCountPanel,
            "TotalFreeSpins");
        portraitRemainingFreeSpinsText = ResolveChildComponent(
            portraitRemainingFreeSpinsText,
            portraitFreeSpinCountPanel,
            "RemainingFreeSpins",
            "RemainingFreeSpin");
        freeSpinTotalWinText = ResolveChildComponent(
            freeSpinTotalWinText,
            freeSpinWinPanel,
            "TotalWin");
        freeSpinStarFountain = freeSpinStarFountain != null
            ? freeSpinStarFountain
            : FindSceneComponent<StarFountain>();

        extraWinPanel = extraWinPanel != null ? extraWinPanel : FindSceneObject("ExtraWin");
        extraWinTitleImage = ResolveChildComponent(
            extraWinTitleImage,
            extraWinPanel,
            "Image");
        extraWinAmountText = ResolveChildComponent(
            extraWinAmountText,
            extraWinPanel,
            "Amount");
        extraWinCoinFountain = ResolveChildComponent(
            extraWinCoinFountain,
            extraWinPanel,
            "CoinFountain");

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
        soundCloseButton = ResolveChildButton(soundCloseButton, soundPanel, "Back", "Close", "SoundBack", "BackButton");
        musicSlider = ResolveChildComponent(musicSlider, soundPanel, "MusicSlider", "Music Slider");
        sfxSlider = ResolveChildComponent(sfxSlider, soundPanel, "SfxSlider", "SFXSlider", "SoundSlider", "Sound Slider");
        musicToggle = ResolveChildComponent(musicToggle, soundPanel, "MusicToggle");
        sfxToggle = ResolveChildComponent(sfxToggle, soundPanel, "SfxToggle", "SFXToggle");

        auto10Button = ResolveChildButton(auto10Button, autoplayPanel, "10");
        auto50Button = ResolveChildButton(auto50Button, autoplayPanel, "50");
        auto100Button = ResolveChildButton(auto100Button, autoplayPanel, "100");
        auto200Button = ResolveChildButton(auto200Button, autoplayPanel, "200");
        auto500Button = ResolveChildButton(auto500Button, autoplayPanel, "500");
        autoInfinityButton = ResolveChildButton(autoInfinityButton, autoplayPanel, "Infinity");

        ResolvePortraitReferences();

        if (gameManager == null) Debug.LogError("[UIManager] GameManager was not found; bottom-panel commands cannot run.");
        if (popupManager == null) Debug.LogWarning("[UIManager] PopupManager was not found; popup blocking is unavailable.");
    }

    private void ResolvePortraitReferences()
    {
        portraitUiRoot = portraitUiRoot != null ? portraitUiRoot : FindSceneObject("PortraitUI");
        if (portraitUiRoot == null)
        {
            Debug.LogWarning("[UIManager] PortraitUI was not found; portrait controls are unavailable.");
            return;
        }

        portraitBalanceText = ResolveChildComponent(portraitBalanceText, portraitUiRoot, "BalanceAmount");
        portraitBetAmountText = ResolveChildComponent(portraitBetAmountText, portraitUiRoot, "BetAmount");
        portraitMiniJackpotText = ResolveChildComponent(
            portraitMiniJackpotText,
            portraitUiRoot,
            "MiniAmount");
        portraitMinorJackpotText = ResolveChildComponent(
            portraitMinorJackpotText,
            portraitUiRoot,
            "MinorAmount");
        portraitMajorJackpotText = ResolveChildComponent(
            portraitMajorJackpotText,
            portraitUiRoot,
            "MajorAmount");
        portraitGrandJackpotText = ResolveChildComponent(
            portraitGrandJackpotText,
            portraitUiRoot,
            "GrandAmount");
        portraitSpinButton = ResolveChildButton(portraitSpinButton, portraitUiRoot, "Spin");
        portraitStopButton = ResolveChildButton(portraitStopButton, portraitUiRoot, "Stop");
        portraitAutoplayStopButton = ResolveChildButton(
            portraitAutoplayStopButton,
            portraitUiRoot,
            "AutoplayStop");
        portraitAutoplayCountText = ResolveChildComponent(
            portraitAutoplayCountText,
            portraitUiRoot,
            "AutoplayCount");
        portraitBetIncreaseButton = ResolveChildButton(
            portraitBetIncreaseButton,
            portraitUiRoot,
            "IncreaseBet",
            "BetIncrease");
        portraitBetDecreaseButton = ResolveChildButton(
            portraitBetDecreaseButton,
            portraitUiRoot,
            "DecreaseBet",
            "BetDecrease");
        portraitNormalSpeedButton = ResolveChildButton(
            portraitNormalSpeedButton,
            portraitUiRoot,
            "NormalSpinSpeed");
        portraitFastSpeedButton = ResolveChildButton(
            portraitFastSpeedButton,
            portraitUiRoot,
            "FastSpinSpeed");
        portraitSkipSpeedButton = ResolveChildButton(
            portraitSkipSpeedButton,
            portraitUiRoot,
            "SkipSpinSpeed");
        portraitBottomFreeSpinStartButton = ResolveChildButton(
            portraitBottomFreeSpinStartButton,
            portraitUiRoot,
            "Start");
        portraitTakeFreeSpinButton = ResolveChildButton(
            portraitTakeFreeSpinButton,
            portraitUiRoot,
            "Take",
            "TAKE");

        portraitAutoplayPanel = FindChildObject(portraitUiRoot, "AutoplayPanel", "Autoplay Panel");
        portraitAuto10Button = ResolveChildButton(portraitAuto10Button, portraitAutoplayPanel, "10");
        portraitAuto50Button = ResolveChildButton(portraitAuto50Button, portraitAutoplayPanel, "50");
        portraitAuto100Button = ResolveChildButton(portraitAuto100Button, portraitAutoplayPanel, "100");
        portraitAuto200Button = ResolveChildButton(portraitAuto200Button, portraitAutoplayPanel, "200");
        portraitAuto500Button = ResolveChildButton(portraitAuto500Button, portraitAutoplayPanel, "500");
        portraitAutoInfinityButton = ResolveChildButton(
            portraitAutoInfinityButton,
            portraitAutoplayPanel,
            "Infinite",
            "Infinity");

        portraitMenuPanel = FindChildObject(portraitUiRoot, "HamburgerMenu");
        portraitHamburgerButton = ResolveChildButton(
            portraitHamburgerButton,
            portraitUiRoot,
            "HambugerMenu",
            "HamburgerIcon");
        portraitMenuCloseButton = ResolveChildButton(portraitMenuCloseButton, portraitUiRoot, "DownIcon");
        portraitHamburgerIcon = portraitHamburgerButton != null ? portraitHamburgerButton.gameObject : null;
        portraitDownIcon = portraitMenuCloseButton != null ? portraitMenuCloseButton.gameObject : null;
        portraitInfoButton = ResolveChildButton(portraitInfoButton, portraitMenuPanel, "Info");
        portraitGuideButton = ResolveChildButton(portraitGuideButton, portraitMenuPanel, "Guide", "Bulb");
        portraitSoundButton = ResolveChildButton(portraitSoundButton, portraitMenuPanel, "Sound");
        portraitHomeButton = ResolveChildButton(portraitHomeButton, portraitMenuPanel, "Home");
        portraitMoreGamesButton = ResolveChildButton(portraitMoreGamesButton, portraitMenuPanel, "MoreGames");
        portraitEnterFullscreenButton = ResolveChildButton(
            portraitEnterFullscreenButton,
            portraitUiRoot,
            "FullScreen");
        portraitExitFullscreenButton = ResolveChildButton(
            portraitExitFullscreenButton,
            portraitUiRoot,
            "SmallScreen");
        GameObject portraitTopPanel = FindChildObject(portraitUiRoot, "TopPanel");
        portraitJackpotTopPanel = portraitTopPanel != null
            ? portraitTopPanel.GetComponent<RectTransform>()
            : null;
    }

    private void PreparePanels()
    {
        autoplayCanvasGroup = EnsureCanvasGroup(autoplayPanel, autoplayCanvasGroup);
        portraitAutoplayCanvasGroup = EnsureCanvasGroup(portraitAutoplayPanel, portraitAutoplayCanvasGroup);
        menuCanvasGroup = EnsureCanvasGroup(menuPanel, menuCanvasGroup);
        portraitMenuCanvasGroup = EnsureCanvasGroup(portraitMenuPanel, portraitMenuCanvasGroup);
        soundCanvasGroup = EnsureCanvasGroup(soundPanel, soundCanvasGroup);
        freeSpinWinCanvasGroup = EnsureCanvasGroup(freeSpinWinPanel, freeSpinWinCanvasGroup);

        if (autoplayPanel != null)
        {
            PrepareAutoplayViewport(
                autoplayPanel,
                ref autoplayViewport,
                ref autoplayRestingPosition,
                ref autoplayRestingPositionCached,
                "AutoplayViewport");
            SetCanvasInteraction(autoplayCanvasGroup, false);
        }

        if (portraitAutoplayPanel != null)
        {
            PrepareAutoplayViewport(
                portraitAutoplayPanel,
                ref portraitAutoplayViewport,
                ref portraitAutoplayRestingPosition,
                ref portraitAutoplayRestingPositionCached,
                "PortraitAutoplayViewport");
            SetCanvasInteraction(portraitAutoplayCanvasGroup, false);
        }

        menuOpen = (menuPanel != null && menuPanel.activeSelf) ||
            (portraitMenuPanel != null && portraitMenuPanel.activeSelf);
        if (soundPanel != null)
        {
            soundPanel.transform.localScale = Vector3.one;
            soundPanel.SetActive(false);
            SetCanvasInteraction(soundCanvasGroup, false);
        }

        soundOpen = false;
        if (freeSpinPanel != null)
        {
            freeSpinPanel.transform.localScale = Vector3.one;
            CacheFreeSpinScaleTarget();
            if (freeSpinScaleTarget != null)
            {
                freeSpinScaleTarget.localScale = freeSpinScaleTargetOriginalScale;
            }
            freeSpinPanel.SetActive(false);
        }
        SetFreeSpinCountPanelVisible(false);
        if (freeSpinWinPanel != null) freeSpinWinPanel.SetActive(false);
        if (freeSpinWinCanvasGroup != null) freeSpinWinCanvasGroup.alpha = 1f;
        SetCanvasInteraction(freeSpinWinCanvasGroup, false);
        if (freeSpinTotalWinText != null)
        {
            freeSpinTotalWinText.text = FormatSpriteAmount(freeSpinTotalWinText, 0d, 0);
        }
        CacheExtraWinOriginalScale();
        ResetExtraWinPresentation(true);
        StopFreeSpinStarFountain();
        HideFreeSpinTransitionOverlay();
        SetVisible(bottomFreeSpinStartButton, false);
        SetVisible(portraitBottomFreeSpinStartButton, false);
        SetFreeSpinTakeButtonsVisible(false);
        ApplyMenuIcons();
    }

    private void PlayFreeSpinOfferTransition()
    {
        CancelFreeSpinOfferTransition();
        if (freeSpinPanel == null)
        {
            audioManager?.PlayFreeGamesMusic();
            SetFreeSpinCountPanelVisible(true);
            freeSpinStarFountain?.PlayStarBurst();
            return;
        }

        CacheFreeSpinScaleTarget();
        freeSpinPanel.transform.localScale = Vector3.one;
        EnsureFreeSpinTransitionOverlay();
        if (freeSpinTransitionOverlay == null)
        {
            audioManager?.PlayFreeGamesMusic();
            SetFreeSpinCountPanelVisible(true);
            freeSpinPanel.SetActive(true);
            freeSpinStarFountain?.PlayStarBurst();

            if (freeSpinScaleTarget == null)
            {
                RefreshControls();
                return;
            }

            freeSpinScaleTarget.localScale = Vector3.zero;
            freeSpinOfferTransitionTween = freeSpinScaleTarget
                .DOScale(freeSpinScaleTargetOriginalScale, freeSpinOfferScaleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => freeSpinOfferTransitionTween = null);
            return;
        }

        freeSpinPanel.SetActive(false);
        if (freeSpinScaleTarget != null) freeSpinScaleTarget.localScale = Vector3.zero;

        freeSpinTransitionOverlay.gameObject.SetActive(true);
        freeSpinTransitionOverlay.transform.SetAsLastSibling();
        freeSpinTransitionOverlay.alpha = 0f;
        freeSpinTransitionOverlay.interactable = true;
        freeSpinTransitionOverlay.blocksRaycasts = true;

        Sequence transition = DOTween.Sequence().SetUpdate(true);
        transition.Append(
            freeSpinTransitionOverlay
                .DOFade(1f, Mathf.Max(MinimumFreeSpinOfferFadeDuration, freeSpinFadeToBlackDuration))
                .SetEase(Ease.Linear));
        transition.AppendCallback(() => audioManager?.PlayFreeGamesMusic());
        transition.AppendInterval(Mathf.Max(1f, freeSpinBlackHoldDuration));
        transition.AppendCallback(() =>
        {
            if (freeSpinPanel == null) return;

            SetFreeSpinCountPanelVisible(true);
            freeSpinPanel.SetActive(true);
            freeSpinPanel.transform.localScale = Vector3.one;
            if (freeSpinScaleTarget != null) freeSpinScaleTarget.localScale = Vector3.zero;
            freeSpinTransitionOverlay.transform.SetAsLastSibling();
            freeSpinStarFountain?.PlayStarBurst();
        });
        transition.Append(
            freeSpinTransitionOverlay
                .DOFade(0f, Mathf.Max(MinimumFreeSpinOfferFadeDuration, freeSpinFadeFromBlackDuration))
                .SetEase(Ease.Linear));
        if (freeSpinScaleTarget != null)
        {
            transition.Join(
                freeSpinScaleTarget
                    .DOScale(freeSpinScaleTargetOriginalScale, freeSpinOfferScaleDuration)
                    .SetEase(Ease.OutBack));
        }
        transition.OnComplete(() =>
        {
            freeSpinOfferTransitionTween = null;
            if (freeSpinPanel != null) freeSpinPanel.transform.localScale = Vector3.one;
            if (freeSpinScaleTarget != null)
            {
                freeSpinScaleTarget.localScale = freeSpinScaleTargetOriginalScale;
            }
            HideFreeSpinTransitionOverlay();
            RefreshControls();
        });
        freeSpinOfferTransitionTween = transition;
    }

    private void CacheFreeSpinScaleTarget()
    {
        if (freeSpinScaleTargetScaleCached || freeSpinScaleTarget == null)
        {
            return;
        }

        freeSpinScaleTargetOriginalScale = freeSpinScaleTarget.localScale;
        freeSpinScaleTargetScaleCached = true;
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

        if (freeSpinPanel != null)
        {
            freeSpinPanel.transform.localScale = Vector3.one;
        }

        if (freeSpinScaleTarget != null && freeSpinScaleTargetScaleCached)
        {
            freeSpinScaleTarget.localScale = freeSpinScaleTargetOriginalScale;
        }

        HideFreeSpinTransitionOverlay();
    }

    private void CancelFreeSpinCollectTransition()
    {
        freeSpinCollectTransitionTween?.Kill();
        freeSpinCollectTransitionTween = null;

        if (freeSpinWinCanvasGroup != null)
        {
            freeSpinWinCanvasGroup.alpha = 1f;
        }

        SetCanvasInteraction(freeSpinWinCanvasGroup, false);
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
        spinPointerDownEntry = CreateEventTriggerEntry(EventTriggerType.PointerDown, HandleSpinPointerDown);
        spinPointerUpEntry = CreateEventTriggerEntry(EventTriggerType.PointerUp, HandleSpinPointerUp);
        spinPointerExitEntry = CreateEventTriggerEntry(EventTriggerType.PointerExit, HandleSpinPointerExit);

        AddSpinEventTrigger(spinButton);
        AddSpinEventTrigger(portraitSpinButton);
    }

    private void AddSpinEventTrigger(Button button)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        if (!spinEventTriggers.Contains(trigger)) spinEventTriggers.Add(trigger);
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
        Bind(collectFreeSpinButton, HandleFreeSpinTakeClick);
        Bind(landscapeTakeFreeSpinButton, HandleFreeSpinTakeClick);
        Bind(portraitTakeFreeSpinButton, HandleFreeSpinTakeClick);
        Bind(betIncreaseButton, HandleBetIncrease);
        Bind(betDecreaseButton, HandleBetDecrease);
        Bind(normalSpeedButton, HandleSpeedClick);
        Bind(fastSpeedButton, HandleSpeedClick);
        Bind(skipSpeedButton, HandleSpeedClick);
        BindAutoplayButtons();
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

        Bind(portraitSpinButton, HandleSpinClick);
        Bind(portraitStopButton, HandleStopClick);
        Bind(portraitAutoplayStopButton, HandleAutoplayStopClick);
        Bind(portraitBottomFreeSpinStartButton, HandleFreeSpinStartClick);
        Bind(portraitBetIncreaseButton, HandleBetIncrease);
        Bind(portraitBetDecreaseButton, HandleBetDecrease);
        Bind(portraitNormalSpeedButton, HandleSpeedClick);
        Bind(portraitFastSpeedButton, HandleSpeedClick);
        Bind(portraitSkipSpeedButton, HandleSpeedClick);
        Bind(portraitHamburgerButton, OpenMenu);
        Bind(portraitMenuCloseButton, CloseMenu);
        Bind(portraitInfoButton, OpenInfo);
        Bind(portraitGuideButton, OpenGuide);
        Bind(portraitSoundButton, OpenSound);
        Bind(portraitHomeButton, HandleHome);
        Bind(portraitMoreGamesButton, HandleMoreGames);
        Bind(portraitEnterFullscreenButton, EnterFullscreen);
        Bind(portraitExitFullscreenButton, ExitFullscreen);

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(HandleMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(HandleSfxVolume);
        if (musicToggle != null) musicToggle.onValueChanged.AddListener(HandleMusicToggle);
        if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(HandleSfxToggle);

        if (gameManager != null)
        {
            gameManager.InitDataReceived += HandleInfoInitDataReceived;
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
        Unbind(collectFreeSpinButton, HandleFreeSpinTakeClick);
        Unbind(landscapeTakeFreeSpinButton, HandleFreeSpinTakeClick);
        Unbind(portraitTakeFreeSpinButton, HandleFreeSpinTakeClick);
        Unbind(betIncreaseButton, HandleBetIncrease);
        Unbind(betDecreaseButton, HandleBetDecrease);
        Unbind(normalSpeedButton, HandleSpeedClick);
        Unbind(fastSpeedButton, HandleSpeedClick);
        Unbind(skipSpeedButton, HandleSpeedClick);
        UnbindAutoplayButtons();
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

        Unbind(portraitSpinButton, HandleSpinClick);
        Unbind(portraitStopButton, HandleStopClick);
        Unbind(portraitAutoplayStopButton, HandleAutoplayStopClick);
        Unbind(portraitBottomFreeSpinStartButton, HandleFreeSpinStartClick);
        Unbind(portraitBetIncreaseButton, HandleBetIncrease);
        Unbind(portraitBetDecreaseButton, HandleBetDecrease);
        Unbind(portraitNormalSpeedButton, HandleSpeedClick);
        Unbind(portraitFastSpeedButton, HandleSpeedClick);
        Unbind(portraitSkipSpeedButton, HandleSpeedClick);
        Unbind(portraitHamburgerButton, OpenMenu);
        Unbind(portraitMenuCloseButton, CloseMenu);
        Unbind(portraitInfoButton, OpenInfo);
        Unbind(portraitGuideButton, OpenGuide);
        Unbind(portraitSoundButton, OpenSound);
        Unbind(portraitHomeButton, HandleHome);
        Unbind(portraitMoreGamesButton, HandleMoreGames);
        Unbind(portraitEnterFullscreenButton, EnterFullscreen);
        Unbind(portraitExitFullscreenButton, ExitFullscreen);

        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(HandleMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSfxVolume);
        if (musicToggle != null) musicToggle.onValueChanged.RemoveListener(HandleMusicToggle);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(HandleSfxToggle);

        if (gameManager != null)
        {
            gameManager.InitDataReceived -= HandleInfoInitDataReceived;
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

    private void BindAutoplayButtons()
    {
        ResolveAutoplayButtonReferences();
        Rebind(auto10Button, HandleAuto10);
        Rebind(auto50Button, HandleAuto50);
        Rebind(auto100Button, HandleAuto100);
        Rebind(auto200Button, HandleAuto200);
        Rebind(auto500Button, HandleAuto500);
        Rebind(autoInfinityButton, HandleAutoInfinity);
        Rebind(portraitAuto10Button, HandleAuto10);
        Rebind(portraitAuto50Button, HandleAuto50);
        Rebind(portraitAuto100Button, HandleAuto100);
        Rebind(portraitAuto200Button, HandleAuto200);
        Rebind(portraitAuto500Button, HandleAuto500);
        Rebind(portraitAutoInfinityButton, HandleAutoInfinity);
    }

    private void UnbindAutoplayButtons()
    {
        Unbind(auto10Button, HandleAuto10);
        Unbind(auto50Button, HandleAuto50);
        Unbind(auto100Button, HandleAuto100);
        Unbind(auto200Button, HandleAuto200);
        Unbind(auto500Button, HandleAuto500);
        Unbind(autoInfinityButton, HandleAutoInfinity);
        Unbind(portraitAuto10Button, HandleAuto10);
        Unbind(portraitAuto50Button, HandleAuto50);
        Unbind(portraitAuto100Button, HandleAuto100);
        Unbind(portraitAuto200Button, HandleAuto200);
        Unbind(portraitAuto500Button, HandleAuto500);
        Unbind(portraitAutoInfinityButton, HandleAutoInfinity);
    }

    private void ResolveAutoplayButtonReferences()
    {
        auto10Button = ResolveChildButton(auto10Button, autoplayPanel, "10");
        auto50Button = ResolveChildButton(auto50Button, autoplayPanel, "50");
        auto100Button = ResolveChildButton(auto100Button, autoplayPanel, "100");
        auto200Button = ResolveChildButton(auto200Button, autoplayPanel, "200");
        auto500Button = ResolveChildButton(auto500Button, autoplayPanel, "500");
        autoInfinityButton = ResolveChildButton(autoInfinityButton, autoplayPanel, "Infinity");
        portraitAuto10Button = ResolveChildButton(portraitAuto10Button, portraitAutoplayPanel, "10");
        portraitAuto50Button = ResolveChildButton(portraitAuto50Button, portraitAutoplayPanel, "50");
        portraitAuto100Button = ResolveChildButton(portraitAuto100Button, portraitAutoplayPanel, "100");
        portraitAuto200Button = ResolveChildButton(portraitAuto200Button, portraitAutoplayPanel, "200");
        portraitAuto500Button = ResolveChildButton(portraitAuto500Button, portraitAutoplayPanel, "500");
        portraitAutoInfinityButton = ResolveChildButton(
            portraitAutoInfinityButton,
            portraitAutoplayPanel,
            "Infinite",
            "Infinity");
    }

    private void HandleSpinPointerDown(BaseEventData eventData)
    {
        if (pointerHeld || !IsAnySpinButtonInteractable() || IsBlockingInteraction) return;
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
        gameManager.TryStartManualSpin();
    }

    private void HandleStopClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        gameManager.RequestStopSpin();
    }

    private void HandleAutoplayStopClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        gameManager.StopAutoSpin();
    }

    private void HandleFreeSpinStartClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        if (gameManager.StartPendingFreeSpins()) audioManager?.PlayFreeSpinButton();
    }

    private void HandleFreeSpinTakeClick()
    {
        if (IsBlockingInteraction || gameManager == null) return;
        if (gameManager.TakeFreeSpinWin()) audioManager?.PlayFreeSpinButton();
    }

    private void HandleBetIncrease() => ChangeBet(true);
    private void HandleBetDecrease() => ChangeBet(false);

    private void ChangeBet(bool increase)
    {
        if (IsBlockingInteraction || gameManager == null || !gameManager.TryChangeBet(increase)) return;
        if (gameManager.IsMaximumBet) audioManager?.PlayMaxBet();
        else audioManager?.PlayBetChange();
    }

    private void HandleSpeedClick()
    {
        if (IsBlockingInteraction || gameManager == null || !gameManager.CycleSpinSpeed()) return;
        audioManager?.PlayTurboClick();
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
        if (!gameManager.StartAutoSpin(count))
        {
            string reason = gameManager.GetAutoplayStartBlockReason(count);
            Debug.LogWarning(
                $"[UIManager] Autoplay selection {FormatAutoplayCount(count)} did not start: " +
                (string.IsNullOrEmpty(reason) ? "the first autoplay round could not begin." : reason));
            RefreshControls();
            return;
        }

        HideAutoplayPanel();
    }

    private static string FormatAutoplayCount(int count)
    {
        return count < 0 ? "Infinity" : count.ToString();
    }

    private void ShowAutoplayPanel()
    {
        if ((autoplayPanel == null && portraitAutoplayPanel == null) ||
            gameManager == null || !gameManager.CanStartManualSpin)
        {
            return;
        }

        CloseMenu(false);
        CloseInfo();
        CloseGuide();

        autoplayTween?.Kill();
        portraitAutoplayTween?.Kill();
        autoplayTween = ShowAutoplayPanelInstance(
            autoplayPanel,
            autoplayCanvasGroup,
            ref autoplayRestingPosition,
            ref autoplayRestingPositionCached);
        portraitAutoplayTween = ShowAutoplayPanelInstance(
            portraitAutoplayPanel,
            portraitAutoplayCanvasGroup,
            ref portraitAutoplayRestingPosition,
            ref portraitAutoplayRestingPositionCached);
        waitForOpeningPointerRelease = true;
        audioManager?.PlayNormalClick();
        RefreshControls();
    }

    private void HideAutoplayPanel()
    {
        autoplayTween?.Kill();
        portraitAutoplayTween?.Kill();
        autoplayTween = HideAutoplayPanelInstance(
            autoplayPanel,
            autoplayCanvasGroup,
            autoplayRestingPosition);
        portraitAutoplayTween = HideAutoplayPanelInstance(
            portraitAutoplayPanel,
            portraitAutoplayCanvasGroup,
            portraitAutoplayRestingPosition);
        RefreshControls();
    }

    private Tween ShowAutoplayPanelInstance(
        GameObject panel,
        CanvasGroup canvasGroup,
        ref Vector3 restingPosition,
        ref bool restingPositionCached)
    {
        if (panel == null) return null;
        RectTransform rect = panel.transform as RectTransform;
        if (rect == null) return null;
        if (!restingPositionCached)
        {
            restingPosition = rect.localPosition;
            restingPositionCached = true;
        }

        bool wasActive = panel.activeSelf;
        panel.SetActive(true);
        float slideDistance = Mathf.Max(40f, rect.rect.height + 2f);
        if (!wasActive)
        {
            rect.localPosition = restingPosition + Vector3.down * slideDistance;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        SetCanvasInteraction(canvasGroup, true);
        return rect.DOLocalMove(restingPosition, autoplaySlideDuration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true)
            .OnComplete(RefreshControls);
    }

    private Tween HideAutoplayPanelInstance(
        GameObject panel,
        CanvasGroup canvasGroup,
        Vector3 restingPosition)
    {
        if (panel == null || !panel.activeSelf) return null;
        RectTransform rect = panel.transform as RectTransform;
        if (rect == null)
        {
            panel.SetActive(false);
            return null;
        }

        SetCanvasInteraction(canvasGroup, false);
        float slideDistance = Mathf.Max(40f, rect.rect.height + 2f);
        Vector3 closedPosition = restingPosition + Vector3.down * slideDistance;
        return rect.DOLocalMove(closedPosition, autoplaySlideDuration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panel.SetActive(false);
                rect.localPosition = restingPosition;
                RefreshControls();
            });
    }

    private static void PrepareAutoplayViewport(
        GameObject panel,
        ref RectTransform viewport,
        ref Vector3 restingPosition,
        ref bool restingPositionCached,
        string viewportName)
    {
        RectTransform panelRect = panel != null ? panel.transform as RectTransform : null;
        if (panelRect == null)
        {
            return;
        }

        if (viewport == null)
        {
            RectTransform existingParent = panelRect.parent as RectTransform;
            if (existingParent != null &&
                existingParent.name == viewportName &&
                existingParent.GetComponent<RectMask2D>() != null)
            {
                viewport = existingParent;
            }
            else
            {
                Transform originalParent = panelRect.parent;
                int originalSiblingIndex = panelRect.GetSiblingIndex();
                Vector2 originalSize = panelRect.rect.size;
                Vector2 originalPivot = panelRect.pivot;

                GameObject viewportObject = new GameObject(
                    viewportName,
                    typeof(RectTransform),
                    typeof(RectMask2D));
                viewportObject.layer = panel.layer;
                viewport = viewportObject.GetComponent<RectTransform>();
                viewport.SetParent(originalParent, false);
                viewport.SetSiblingIndex(originalSiblingIndex);
                viewport.anchorMin = panelRect.anchorMin;
                viewport.anchorMax = panelRect.anchorMax;
                viewport.pivot = originalPivot;
                viewport.sizeDelta = panelRect.sizeDelta;
                viewport.anchoredPosition3D = panelRect.anchoredPosition3D;
                viewport.localRotation = panelRect.localRotation;
                viewport.localScale = panelRect.localScale;

                panelRect.SetParent(viewport, false);
                panelRect.anchorMin = originalPivot;
                panelRect.anchorMax = originalPivot;
                panelRect.pivot = originalPivot;
                panelRect.sizeDelta = originalSize;
                panelRect.anchoredPosition3D = Vector3.zero;
                panelRect.localRotation = Quaternion.identity;
                panelRect.localScale = Vector3.one;
            }
        }

        restingPosition = panelRect.localPosition;
        restingPositionCached = true;
    }

    private void OpenMenu()
    {
        if ((menuPanel == null && portraitMenuPanel == null) || menuOpen || IsBlockingInteraction) return;
        HideAutoplayPanel();
        menuTween?.Kill();
        portraitMenuTween?.Kill();
        menuOpen = true;
        menuTween = OpenMenuPanelInstance(menuPanel, menuCanvasGroup);
        portraitMenuTween = OpenMenuPanelInstance(portraitMenuPanel, portraitMenuCanvasGroup);
        ApplyMenuIcons();
        audioManager?.PlayNormalClick();
        RefreshControls();
    }

    private void CloseMenu() => CloseMenu(true);

    private void CloseMenu(bool animate)
    {
        if (!menuOpen &&
            (menuPanel == null || !menuPanel.activeSelf) &&
            (portraitMenuPanel == null || !portraitMenuPanel.activeSelf))
        {
            return;
        }

        menuTween?.Kill();
        portraitMenuTween?.Kill();
        SetCanvasInteraction(menuCanvasGroup, false);
        SetCanvasInteraction(portraitMenuCanvasGroup, false);
        menuOpen = false;
        menuTween = CloseMenuPanelInstance(menuPanel, menuCanvasGroup, animate);
        portraitMenuTween = CloseMenuPanelInstance(portraitMenuPanel, portraitMenuCanvasGroup, animate);
        ApplyMenuIcons();
        RefreshControls();
    }

    private Tween OpenMenuPanelInstance(GameObject panel, CanvasGroup canvasGroup)
    {
        if (panel == null) return null;
        panel.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        SetCanvasInteraction(canvasGroup, true);
        if (canvasGroup == null || menuFadeDuration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            return null;
        }

        return canvasGroup
            .DOFade(1f, menuFadeDuration)
            .SetUpdate(true)
            .OnComplete(RefreshControls);
    }

    private Tween CloseMenuPanelInstance(GameObject panel, CanvasGroup canvasGroup, bool animate)
    {
        if (panel == null || !panel.activeSelf) return null;
        Action finish = () =>
        {
            panel.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            RefreshControls();
        };

        if (animate && canvasGroup != null && menuFadeDuration > 0f)
        {
            return canvasGroup
                .DOFade(0f, menuFadeDuration)
                .SetUpdate(true)
                .OnComplete(() => finish());
        }

        finish();
        return null;
    }

    private void ApplyMenuIcons()
    {
        if (hamburgerIcon != null) hamburgerIcon.SetActive(!menuOpen);
        if (portraitHamburgerIcon != null) portraitHamburgerIcon.SetActive(!menuOpen);
        if (downIcon != null) downIcon.SetActive(menuOpen);
        if (portraitDownIcon != null) portraitDownIcon.SetActive(menuOpen);
    }

    private void OpenInfo()
    {
        if (infoPanel == null)
        {
            Debug.LogWarning("[UIManager] No Info page is assigned; the existing Info button remains a safe hook.");
            return;
        }

        ApplyInfoPageValues(gameManager != null && gameManager.IsInitialized
            ? gameManager.GameConfig
            : null);
        OpenSimplePanel(infoPanel, guidePanel);
    }

    private void CloseInfo()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        RefreshControls();
    }

    private void HandleInfoInitDataReceived(
        GameConfig config,
        PlayerData playerData,
        List<List<int>> initialMatrix)
    {
        appliedInfoConfig = null;
        infoPageValuesApplied = false;
        ApplyInfoPageValues(config);
    }

    private void ApplyInfoPageValues(GameConfig config)
    {
        if (config == null || infoPanel == null)
        {
            return;
        }

        if (infoPageValuesApplied && ReferenceEquals(appliedInfoConfig, config))
        {
            return;
        }

        UpdateInfoLineCount(config.paylineCount);
        UpdateInfoFeatureValues(config);

        int updatedSymbols = 0;
        if (config.symbols == null || config.symbols.Count == 0)
        {
            Debug.LogWarning("[UIManager] Init did not provide symbol paytable values; the Info page defaults were retained.");
        }
        else
        {
            foreach (SymbolInfo symbol in config.symbols)
            {
                if (TryUpdateInfoSymbolPaytable(symbol, config))
                {
                    updatedSymbols++;
                }
            }
        }

        appliedInfoConfig = config;
        infoPageValuesApplied = true;
        Debug.Log($"[UIManager] Updated {updatedSymbols} Info page symbol paytables from init data.");
    }

    private bool TryUpdateInfoSymbolPaytable(SymbolInfo symbol, GameConfig config)
    {
        if (symbol == null)
        {
            Debug.LogWarning("[UIManager] Ignored a null symbol while updating the Info page.");
            return false;
        }

        if (symbol.multipliers == null || symbol.multipliers.Count == 0)
        {
            Debug.LogWarning($"[UIManager] Init symbol '{symbol.name}' has no payout values; its Info page defaults were retained.");
            return false;
        }

        string sectionName = ResolveInfoSymbolSectionName(symbol, config);
        if (string.IsNullOrEmpty(sectionName))
        {
            Debug.LogWarning($"[UIManager] Init symbol '{symbol.name}' could not be mapped to an Info page section.");
            return false;
        }

        GameObject section = FindChildObject(infoPanel, sectionName);
        if (section == null)
        {
            Debug.LogWarning($"[UIManager] Info page section '{sectionName}' was not found for init symbol '{symbol.name}'.");
            return false;
        }

        TMP_Text[] sectionTexts = section.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text multiplierText = sectionTexts.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.transform.parent == section.transform &&
            candidate.name.StartsWith("Multiplier", StringComparison.OrdinalIgnoreCase));
        if (multiplierText == null)
        {
            Debug.LogWarning($"[UIManager] Info page section '{sectionName}' has no multiplier text field.");
            return false;
        }

        multiplierText.text = BuildInfoColumnText(
            symbol.multipliers.Select(FormatInfoNumber));

        TMP_Text matchCountText = sectionTexts.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.transform.parent == section.transform &&
            IsIntegerInfoColumn(candidate.text));
        if (matchCountText == null)
        {
            Debug.LogWarning($"[UIManager] Info page section '{sectionName}' has no match-count text field.");
            return true;
        }

        List<int> matchCounts = symbol.matchCounts != null &&
            symbol.matchCounts.Count == symbol.multipliers.Count
                ? symbol.matchCounts
                : BuildFallbackMatchCounts(symbol, config.reelCount);
        matchCountText.text = BuildInfoColumnText(
            matchCounts.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        return true;
    }

    private static List<int> BuildFallbackMatchCounts(SymbolInfo symbol, int reelCount)
    {
        int payoutCount = symbol?.multipliers?.Count ?? 0;
        int safeReelCount = Math.Max(1, reelCount);
        int minMatch = symbol != null && symbol.minMatch > 0
            ? Math.Min(symbol.minMatch, safeReelCount)
            : Math.Max(1, safeReelCount - payoutCount + 1);
        int highestMatch = Math.Min(safeReelCount, minMatch + payoutCount - 1);
        var matchCounts = new List<int>();
        for (int index = 0; index < payoutCount; index++)
        {
            matchCounts.Add(Math.Max(minMatch, highestMatch - index));
        }

        return matchCounts;
    }

    private static string ResolveInfoSymbolSectionName(SymbolInfo symbol, GameConfig config)
    {
        string normalizedName = NormalizeInfoSymbolName(symbol?.name);
        switch (normalizedName)
        {
            case "santa":
            case "santawild":
            case "expandingwild":
            case "expandingsantawild":
                return "ExpandingWild";
            case "gift":
            case "giftbox":
            case "giftwild":
            case "present":
            case "presentwild":
                return "Wild";
            case "scatter":
            case "moon":
                return "Scatter";
            case "reindeer":
            case "deer":
                return "DeerSymbol";
            case "bell":
            case "bells":
                return "BellSymbol";
            case "stocking":
            case "stockings":
            case "sock":
            case "socks":
                return "SockSymbol";
            case "candle":
                return "CandleSymbol";
            case "milkcookie":
            case "milkcookies":
            case "cookie":
            case "cookies":
            case "cup":
                return "CupSymbol";
            case "ace":
            case "a":
                return "AceSymbol";
            case "king":
            case "k":
                return "KingSymbol";
            case "queen":
            case "q":
                return "QueenSymbol";
            case "jack":
            case "j":
                return "JackSymbol";
            case "ten":
            case "10":
                return "TenSymbol";
            case "wild":
                if (config != null && symbol.id == config.giftWildSymbolId)
                {
                    return "Wild";
                }

                if (config != null && symbol.id == config.expandingWildSymbolId)
                {
                    return "ExpandingWild";
                }

                return null;
            default:
                return null;
        }
    }

    private static string NormalizeInfoSymbolName(string symbolName)
    {
        return string.IsNullOrWhiteSpace(symbolName)
            ? string.Empty
            : new string(symbolName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static bool IsIntegerInfoColumn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(token =>
            int.TryParse(
                token.Trim('\''),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _));
    }

    private static string BuildInfoColumnText(IEnumerable<string> values)
    {
        return string.Join("\n", values);
    }

    private static string FormatInfoNumber(double value)
    {
        double rounded = Math.Round(value);
        return Math.Abs(value - rounded) < 0.0000001d
            ? rounded.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.################", CultureInfo.InvariantCulture);
    }

    private void UpdateInfoLineCount(int paylineCount)
    {
        if (paylineCount <= 0 || infoPanel == null)
        {
            return;
        }

        string formattedCount = paylineCount.ToString(CultureInfo.InvariantCulture);
        foreach (TMP_Text text in infoPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                continue;
            }

            text.text = Regex.Replace(
                text.text,
                @"\d+(?=\s+(?:LINES|WAYS\s+TO\s+WIN)\b)",
                formattedCount,
                RegexOptions.IgnoreCase);
        }
    }

    private void UpdateInfoFeatureValues(GameConfig config)
    {
        Features features = config?.features;
        if (features == null)
        {
            return;
        }

        int scatterTriggerCount = features.scatter != null
            ? features.scatter.minTriggerCount
            : 0;
        ReplaceInfoScatterTriggerCount(
            FindChildObject(infoPanel, "Scatter"),
            scatterTriggerCount);

        int freeGameTriggerCount = features.freeGames != null && features.freeGames.triggerCount > 0
            ? features.freeGames.triggerCount
            : scatterTriggerCount;
        ReplaceInfoScatterTriggerCount(
            FindChildObject(infoPanel, "FreeGames"),
            freeGameTriggerCount);

        UpdateInfoMultiplierWildValues(features.multiplierWilds);
        UpdateInfoExpandingWildValues(features.expandingWild, config.reelCount);
    }

    private static void ReplaceInfoScatterTriggerCount(GameObject section, int triggerCount)
    {
        if (section == null || triggerCount <= 0)
        {
            return;
        }

        string formattedCount = triggerCount.ToString(CultureInfo.InvariantCulture);
        foreach (TMP_Text text in section.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                continue;
            }

            text.text = Regex.Replace(
                text.text,
                @"\d+(?=\s+or\s+more\s+Scatters?)",
                formattedCount,
                RegexOptions.IgnoreCase);
        }
    }

    private void UpdateInfoMultiplierWildValues(MultiplierWilds multiplierWilds)
    {
        GiftWildCountMultiplier configuredValues = multiplierWilds?.giftWildCountMultiplier;
        GameObject section = FindChildObject(infoPanel, "MultiplierWild");
        if (configuredValues == null || section == null)
        {
            return;
        }

        int[] values = { configuredValues._1, configuredValues._2 };
        int valueIndex = 0;
        foreach (TMP_Text text in section.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || string.IsNullOrEmpty(text.text) || valueIndex >= values.Length)
            {
                continue;
            }

            text.text = Regex.Replace(
                text.text,
                @"[xX]\s*\d+",
                match =>
                {
                    if (valueIndex >= values.Length || values[valueIndex] <= 0)
                    {
                        valueIndex++;
                        return match.Value;
                    }

                    char prefix = match.Value[0];
                    return prefix + values[valueIndex++].ToString(CultureInfo.InvariantCulture);
                });
        }
    }

    private void UpdateInfoExpandingWildValues(ExpandingWild expandingWild, int reelCount)
    {
        if (expandingWild == null)
        {
            return;
        }

        List<int> allConfiguredReels = new List<int>();
        if (expandingWild.baseGameReels != null)
        {
            allConfiguredReels.AddRange(expandingWild.baseGameReels);
        }
        if (expandingWild.freeGameReels != null)
        {
            allConfiguredReels.AddRange(expandingWild.freeGameReels);
        }

        bool zeroBasedReels = allConfiguredReels.Contains(0) ||
            (allConfiguredReels.Count > 0 && !allConfiguredReels.Contains(Math.Max(1, reelCount)));
        List<int> baseGameReels = NormalizeInfoReelNumbers(
            expandingWild.baseGameReels,
            reelCount,
            zeroBasedReels);
        int freeGameReelCount = NormalizeInfoReelNumbers(
                expandingWild.freeGameReels,
                reelCount,
                zeroBasedReels)
            .Count;

        foreach (string sectionName in new[] { "ExpandingWild", "ExpandingSanta" })
        {
            GameObject section = FindChildObject(infoPanel, sectionName);
            if (section == null)
            {
                continue;
            }

            foreach (TMP_Text text in section.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                if (baseGameReels.Count > 0)
                {
                    text.text = Regex.Replace(
                        text.text,
                        @"reels\s+\d+(?:\s*(?:,|and)\s*\d+)+\s+only",
                        "reels " + FormatInfoNumberList(baseGameReels) + " only",
                        RegexOptions.IgnoreCase);
                }

                if (freeGameReelCount > 0)
                {
                    text.text = Regex.Replace(
                        text.text,
                        @"all\s+\d+\s+reels",
                        "all " + freeGameReelCount.ToString(CultureInfo.InvariantCulture) + " reels",
                        RegexOptions.IgnoreCase);
                }
            }
        }
    }

    private static List<int> NormalizeInfoReelNumbers(
        IEnumerable<int> configuredReels,
        int reelCount,
        bool zeroBased)
    {
        if (configuredReels == null)
        {
            return new List<int>();
        }

        int safeReelCount = Math.Max(1, reelCount);
        return configuredReels
            .Select(value => zeroBased ? value + 1 : value)
            .Where(value => value >= 1 && value <= safeReelCount)
            .Distinct()
            .OrderBy(value => value)
            .ToList();
    }

    private static string FormatInfoNumberList(IReadOnlyList<int> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        if (values.Count == 1)
        {
            return values[0].ToString(CultureInfo.InvariantCulture);
        }

        if (values.Count == 2)
        {
            return values[0].ToString(CultureInfo.InvariantCulture) +
                " and " +
                values[1].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(", ", values.Take(values.Count - 1)) +
            " and " +
            values[values.Count - 1].ToString(CultureInfo.InvariantCulture);
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
        ResetPanelToFirstPage(target);
        audioManager?.PlayPopupOpen();
        RefreshControls();
    }

    private static void ResetPanelToFirstPage(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        foreach (ScrollRect scrollRect in panel.GetComponentsInChildren<ScrollRect>(true))
        {
            if (scrollRect == null)
            {
                continue;
            }

            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
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
        if (IsBlockingInteraction || gameManager == null || gameManager.IsAutoplayActive ||
            gameManager.IsAutoplayRoundSettling)
        {
            return;
        }

        if (popupManager == null)
        {
            Debug.LogError("[UIManager] Exit confirmation could not open because PopupManager is missing.");
            return;
        }

        CloseMenu(false);
        popupManager.ShowExitConfirmationPopup();
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
        if (jsBridge == null)
        {
            Debug.LogWarning("[Fullscreen] Cannot request fullscreen because JSFunctCalls is missing.");
            fullscreenState = false;
            ApplyFullscreenButtonState();
            return;
        }

        // This must remain a direct synchronous call from the button handler so
        // requestFullscreen keeps the browser's user-gesture permission.
        jsBridge.RequestExpandGame();
    }

    private void ExitFullscreen()
    {
        if (IsBlockingInteraction) return;
        if (jsBridge == null)
        {
            Debug.LogWarning("[Fullscreen] Cannot exit fullscreen because JSFunctCalls is missing.");
            return;
        }

        jsBridge.RequestShrinkGame();
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
        bool countPanelWasVisible = IsFreeSpinCountPanelVisible();
        isMobilePortrait = mode == OrientationChange.OrientationMode.MobilePortrait;
        ApplyExtraWinPanelSize();
        if (listenersRegistered) BindAutoplayButtons();
        UpdatePortraitJackpotBobState();
        SetFreeSpinCountPanelVisible(countPanelWasVisible);
        CancelSpinHold(true);
        RefreshControls();
    }

    private void InitializeExtraWinOrientation()
    {
        OrientationChange orientationChange = FindSceneComponent<OrientationChange>();
        bool mobileDevice = orientationChange != null
            ? orientationChange.IsMobileDevice()
            : Application.isMobilePlatform;
        isMobilePortrait = Screen.height > Screen.width && mobileDevice;
        ApplyExtraWinPanelSize();
    }

    private void ApplyExtraWinPanelSize()
    {
        RectTransform extraWinRect = extraWinPanel != null
            ? extraWinPanel.transform as RectTransform
            : null;
        if (extraWinRect == null) return;

        extraWinRect.sizeDelta = isMobilePortrait
            ? extraWinPortraitSize
            : extraWinLandscapeSize;
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
               IsPointerInside(portraitAutoplayPanel, pointer) ||
               IsPointerInside(betIncreaseButton != null ? betIncreaseButton.gameObject : null, pointer) ||
               IsPointerInside(portraitBetIncreaseButton != null ? portraitBetIncreaseButton.gameObject : null, pointer) ||
               IsPointerInside(betDecreaseButton != null ? betDecreaseButton.gameObject : null, pointer) ||
               IsPointerInside(portraitBetDecreaseButton != null ? portraitBetDecreaseButton.gameObject : null, pointer) ||
               IsPointerInside(normalSpeedButton != null ? normalSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(portraitNormalSpeedButton != null ? portraitNormalSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(fastSpeedButton != null ? fastSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(portraitFastSpeedButton != null ? portraitFastSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(skipSpeedButton != null ? skipSpeedButton.gameObject : null, pointer) ||
               IsPointerInside(portraitSkipSpeedButton != null ? portraitSkipSpeedButton.gameObject : null, pointer);
    }

    private bool IsAnyAutoplayPanelOpen()
    {
        return (autoplayPanel != null && autoplayPanel.activeInHierarchy) ||
            (portraitAutoplayPanel != null && portraitAutoplayPanel.activeInHierarchy);
    }

    private bool IsAnySpinButtonInteractable()
    {
        return (spinButton != null && spinButton.gameObject.activeInHierarchy && spinButton.interactable) ||
            (portraitSpinButton != null && portraitSpinButton.gameObject.activeInHierarchy && portraitSpinButton.interactable);
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
        ResetExtraWinPresentation(true);
        StopPortraitJackpotBob(true);
        CancelFreeSpinTotalWinCount();
        CancelFreeSpinCollectTransition();
        CancelFreeSpinOfferTransition();
        autoplayTween?.Kill();
        portraitAutoplayTween?.Kill();
        menuTween?.Kill();
        portraitMenuTween?.Kill();
        soundTween?.Kill();
        autoplayTween = null;
        portraitAutoplayTween = null;
        menuTween = null;
        portraitMenuTween = null;
        soundTween = null;
    }

    private void UpdatePortraitJackpotBobState()
    {
        if (isMobilePortrait)
        {
            if (portraitJackpotBobTween == null || !portraitJackpotBobTween.IsActive())
            {
                StartPortraitJackpotBob();
            }
        }
        else
        {
            StopPortraitJackpotBob(true);
        }
    }

    private void StartPortraitJackpotBob()
    {
        StopPortraitJackpotBob(true);

        if (portraitJackpotTopPanel == null || portraitJackpotBobDistance <= 0f)
        {
            return;
        }

        RectTransform[] descendants = portraitJackpotTopPanel.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform descendant in descendants)
        {
            if (descendant != portraitJackpotTopPanel && IsJackpotPanelName(descendant.name))
            {
                portraitJackpotTargets.Add(descendant);
                portraitJackpotStartingPositions.Add(descendant.anchoredPosition);
            }
        }

        if (portraitJackpotTargets.Count == 0)
        {
            return;
        }

        float phase = 0f;
        portraitJackpotBobTween = DOTween.To(
                () => phase,
                value =>
                {
                    phase = value;
                    ApplyPortraitJackpotVerticalOffset(Mathf.Sin(value) * portraitJackpotBobDistance);
                },
                FullCircleRadians,
                Mathf.Max(0.01f, portraitJackpotBobCycleDuration))
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetTarget(this);
    }

    private static bool IsJackpotPanelName(string objectName)
    {
        return objectName == "Grand"
            || objectName == "Major"
            || objectName == "Minor"
            || objectName == "Mini";
    }

    private void ApplyPortraitJackpotVerticalOffset(float offset)
    {
        for (int i = 0; i < portraitJackpotTargets.Count; i++)
        {
            RectTransform target = portraitJackpotTargets[i];
            if (target != null)
            {
                target.anchoredPosition = portraitJackpotStartingPositions[i] + Vector2.up * offset;
            }
        }
    }

    private void StopPortraitJackpotBob(bool restorePositions)
    {
        if (portraitJackpotBobTween != null && portraitJackpotBobTween.IsActive())
        {
            portraitJackpotBobTween.Kill();
        }

        portraitJackpotBobTween = null;

        if (restorePositions)
        {
            ApplyPortraitJackpotVerticalOffset(0f);
        }

        portraitJackpotTargets.Clear();
        portraitJackpotStartingPositions.Clear();
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

    private bool IsFreeSpinCountPanelVisible()
    {
        return (freeSpinCountPanel != null && freeSpinCountPanel.activeSelf) ||
            (portraitFreeSpinCountPanel != null && portraitFreeSpinCountPanel.activeSelf);
    }

    private void SetFreeSpinCountPanelVisible(bool visible)
    {
        if (freeSpinCountPanel != null)
        {
            freeSpinCountPanel.SetActive(visible && !isMobilePortrait);
        }

        if (portraitFreeSpinCountPanel != null)
        {
            portraitFreeSpinCountPanel.SetActive(visible && isMobilePortrait);
        }
    }

    private void SetFreeSpinTakeButtonsVisible(bool visible)
    {
        SetVisible(collectFreeSpinButton, visible);
        SetVisible(landscapeTakeFreeSpinButton, visible);
        SetVisible(portraitTakeFreeSpinButton, visible);
    }

    private void SetFreeSpinTakeButtonsInteractable(bool interactable)
    {
        if (collectFreeSpinButton != null) collectFreeSpinButton.interactable = interactable;
        if (landscapeTakeFreeSpinButton != null) landscapeTakeFreeSpinButton.interactable = interactable;
        if (portraitTakeFreeSpinButton != null) portraitTakeFreeSpinButton.interactable = interactable;
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

    private static void UpdateAutoplayCount(TMP_Text target, bool visible, string value)
    {
        if (target == null) return;
        target.gameObject.SetActive(visible);
        target.text = value;
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
        foreach (EventTrigger trigger in spinEventTriggers)
        {
            if (trigger == null) continue;
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
            AddTriggerIfMissing(trigger, spinPointerDownEntry);
            AddTriggerIfMissing(trigger, spinPointerUpEntry);
            AddTriggerIfMissing(trigger, spinPointerExitEntry);
        }
    }

    private void UnregisterSpinPointerEvents()
    {
        foreach (EventTrigger trigger in spinEventTriggers)
        {
            if (trigger == null || trigger.triggers == null) continue;
            trigger.triggers.Remove(spinPointerDownEntry);
            trigger.triggers.Remove(spinPointerUpEntry);
            trigger.triggers.Remove(spinPointerExitEntry);
        }
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

    private static void Rebind(Button button, UnityAction listener)
    {
        if (button == null) return;
        button.onClick.RemoveListener(listener);
        button.onClick.AddListener(listener);
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

    private static IEnumerable<T> FindNamedComponents<T>(params string[] names) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid() && names.Contains(candidate.name));
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

    private static GameObject FindChildObject(GameObject root, params string[] names)
    {
        if (root == null) return null;
        return root
            .GetComponentsInChildren<Transform>(true)
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => names.Contains(candidate.name));
    }
}
