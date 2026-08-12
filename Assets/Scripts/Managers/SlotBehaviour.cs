using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public sealed class SpinResultEvent : UnityEvent<SpinResult>
{
}

[Serializable]
public sealed class WinTierEvent : UnityEvent<int, double>
{
}

/// <summary>
/// Owns the complete visual slot flow. The server remains authoritative for the
/// matrix, wins, balance, free games, expanding wilds and gift wilds.
///
/// The current scene intentionally has no SlotBehaviour component wired to it.
/// To keep this change isolated to this file, a runtime component is added to
/// the existing SlotManager object and its references are resolved by name.
/// Inspector references still take priority when the component is wired later.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class SlotBehaviour : MonoBehaviour
{
    private const int DefaultReelCount = 5;
    private const int DefaultRowCount = 3;
    private const int InfiniteAutoplay = -1;

    [Header("Sprites")]
    [Tooltip("Symbol sprites in server symbol-id order. If empty, the first reel strip is used.")]
    [SerializeField] private Sprite[] myImages = Array.Empty<Sprite>();

    [Header("Slot Images")]
    [SerializeField] private List<SlotImage> images = new List<SlotImage>();
    [SerializeField] private List<SlotImage> Tempimages = new List<SlotImage>();

    [Header("Slot Elements")]
    [SerializeField] private LayoutElement[] Slot_Elements = Array.Empty<LayoutElement>();

    [Header("Slot Transforms")]
    [SerializeField] private Transform[] Slot_Transform = Array.Empty<Transform>();
    [SerializeField] private Transform reelsRoot;

    [Header("Buttons")]
    [SerializeField] private Button SlotStart_Button;
    [SerializeField] private Button AutoSpinStop_Button;
    [SerializeField] private Button TBetPlus_Button;
    [SerializeField] private Button TBetMinus_Button;
    [SerializeField] private Button StopSpin_Button;
    [SerializeField] private Button NormalSpeed_Button;
    [SerializeField] private Button FastSpeed_Button;
    [SerializeField] private Button SkipSpeed_Button;

    [Header("Autoplay")]
    [SerializeField] private GameObject AutoPlayPanel;
    [SerializeField] private TMP_Text AutoPlayCount_Text;
    [SerializeField, Min(0f)] private float spinButtonHoldDuration = 3f;
    [SerializeField, Min(0f)] private float autoSpinGap = 0.25f;
    [SerializeField, Min(0f)] private float autoSpinWinGap = 0.9f;

    [Header("Miscellaneous UI")]
    [SerializeField] private TMP_Text Balance_text;
    [SerializeField] private TMP_Text TotalBet_text;
    [SerializeField] private TMP_Text LineBet_text;
    [SerializeField] private TMP_Text TotalWin_text;
    [SerializeField] private TMP_Text WinLinesCount_Text;

    [Header("Free Spins")]
    [SerializeField] private GameObject FreeSpinPanel;
    [SerializeField] private Button FreeSpinStart_Button;
    [SerializeField] private GameObject FreeSpinCountPanel;
    [SerializeField] private Image FreeSpinCounter_Image;
    [SerializeField] private Image FreeSpinTotal_Image;
    [SerializeField] private GameObject FreeSpinWinPanel;
    [SerializeField] private Button Take_Button;

    [Header("Spin Timing")]
    [SerializeField, Min(0.1f)] private float normalMinimumSpinTime = 1.35f;
    [SerializeField, Min(0.05f)] private float fastMinimumSpinTime = 0.55f;
    [SerializeField, Min(1)] private int minSpinCyclesBeforeStop = 2;
    [SerializeField, Min(0f)] private float reelStartStagger = 0.06f;
    [SerializeField, Min(0f)] private float reelStopStagger = 0.12f;
    [SerializeField, Min(0.1f)] private float resultTimeout = 12f;
    [SerializeField, Min(100f)] private float normalReelSpeed = 3600f;
    [SerializeField, Min(100f)] private float fastReelSpeed = 7000f;
    [SerializeField, Min(0f)] private float stopAnticipationDistance = 20f;
    [SerializeField, Min(0f)] private float stopOvershootDistance = 35f;
    [SerializeField, Min(0.01f)] private float stopOvershootDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float stopSettleDuration = 0.22f;

    [Header("Win Presentation")]
    [SerializeField, Min(0.01f)] private float allWinsDuration = 0.8f;
    [SerializeField, Min(0.01f)] private float singleWinLineDuration = 0.7f;
    [SerializeField, Min(1f)] private float winningSymbolScale = 1.12f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource buttonAudio;
    [SerializeField] private AudioSource spinAudio;
    [SerializeField] private AudioSource winAudio;

    [Header("Server")]
    [SerializeField] private SocketIOManager SocketManager;

    [Header("Behaviour Events (Optional)")]
    [SerializeField] private UnityEvent onSpinStarted = new UnityEvent();
    [SerializeField] private SpinResultEvent onSpinStopped = new SpinResultEvent();
    [SerializeField] private SpinResultEvent onFreeSpinsTriggered = new SpinResultEvent();
    [SerializeField] private UnityEvent onInsufficientBalance = new UnityEvent();
    [SerializeField] private UnityEvent onSpinRequestFailed = new UnityEvent();
    [SerializeField] private WinTierEvent onWinTier = new WinTierEvent();

    // Kept for compatibility with the previous controller and popup integrations.
    internal bool IsAutoSpin;
    internal bool IsFreeSpin;
    internal bool CheckPopups;
    internal int BetCounter;
    internal bool WasAutoSpinOn;
    internal bool socketConnected;

    public bool IsCurrentlySpinning => IsSpinning;
    public SpinSpeed CurrentSpinSpeed => spinSpeed;

    private readonly List<ReelRuntime> reels = new List<ReelRuntime>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly List<Tween> winTweens = new List<Tween>();
    private readonly Dictionary<int, Sprite> freeSpinNumberSprites = new Dictionary<int, Sprite>();
    private readonly List<Sprite> generatedFreeSpinNumberSprites = new List<Sprite>();

    private static readonly Rect[] FreeSpinNumberRects =
    {
        new Rect(29f, 289f, 85f, 90f),
        new Rect(174f, 289f, 64f, 89f),
        new Rect(298f, 289f, 72f, 89f),
        new Rect(430f, 289f, 71f, 91f),
        new Rect(33f, 158f, 77f, 88f),
        new Rect(168f, 158f, 69f, 88f),
        new Rect(296f, 158f, 76f, 89f),
        new Rect(430f, 158f, 71f, 85f),
        new Rect(34f, 23f, 75f, 91f),
        new Rect(168f, 24f, 76f, 89f),
        new Rect(315f, 49f, 40f, 39f),
        new Rect(448f, 46f, 34f, 45f)
    };

    private GameConfig gameConfig;
    private PlayerData playerData = new PlayerData();
    private GameManager gameManager;
    private SpinResult pendingResult;

    private Coroutine spinRoutine;
    private Coroutine autoSpinRoutine;
    private Coroutine freeSpinRoutine;
    private Coroutine spinButtonHoldRoutine;
    private Coroutine winAnimationRoutine;
    private Tween balanceTween;
    private Tween winAmountTween;

    private bool gameManagerEventsBound;
    private bool isInitialized;
    private bool IsSpinning;
    private bool stopSpinRequested;
    private bool resultReceived;
    private bool resultFailed;
    private bool waitingForLateResult;
    private bool waitingForFreeSpinStart;
    private bool waitingForFreeSpinTake;
    private bool freeSpinNumberRangeWarningShown;
    private bool isSpinButtonPointerDown;
    private bool spinButtonHoldTriggered;
    private bool shuttingDown;

    private int autoplaySpinsRemaining;
    private int freeSpinsRemaining;
    private int freeSpinsUsed;
    private int freeSpinsTotal;
    private double currentBalance;
    private double currentLineBet;
    private double currentTotalBet;
    private double freeSpinTotalWin;
    private SpinSpeed spinSpeed = SpinSpeed.Normal;

    private sealed class ReelRuntime
    {
        internal RectTransform transform;
        internal readonly List<Image> symbols = new List<Image>();
        internal Vector2 restingPosition;
        internal float symbolPitch;
        internal Tween motionTween;
        internal int completedCycles;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeController()
    {
        if (FindSceneComponent<SlotBehaviour>() != null)
        {
            return;
        }

        GameObject slotManagerObject = FindSceneObject("SlotManager");
        if (slotManagerObject != null)
        {
            slotManagerObject.AddComponent<SlotBehaviour>();
        }
        else
        {
            Debug.LogWarning("[SlotBehaviour] SlotManager was not found; the slot controller was not created.");
        }
    }

    private void Awake()
    {
        ResolveSceneReferences();
        BindGameManagerEvents();
        BuildReelCache();
        BindButtons();
        SetSpeed(spinSpeed, false);
        SetControlState();
    }

    private void Start()
    {
        if (!isInitialized && gameManager != null && gameManager.isInitialized)
        {
            ApplyInitialization(
                gameManager.gameConfig,
                gameManager.playerData,
                GenerateRandomMatrix(gameManager.gameConfig != null ? gameManager.gameConfig.rowCount : DefaultRowCount));
        }
    }

    private void Update()
    {
        if (IsSpinning && SocketManager != null && !SocketManager.isConnected)
        {
            resultFailed = true;
        }

        // balance:sync updates GameManager directly. Mirror it while idle without
        // interfering with the server balance applied at the end of a spin.
        if (!IsSpinning && isInitialized)
        {
            if (gameManager != null && gameManager.playerData != null &&
                Math.Abs(gameManager.playerData.balance - currentBalance) > 0.000001d)
            {
                UpdateBalanceDisplay(gameManager.playerData.balance);
            }
        }
    }

    private void OnEnable()
    {
        shuttingDown = false;
        BindGameManagerEvents();
    }

    private void OnDisable()
    {
        shuttingDown = true;
        UnbindGameManagerEvents();
        CancelSpinButtonHold();
        KillAllTweens();
        StopWinningAnimations();

        if (spinAudio != null && spinAudio.isPlaying)
        {
            spinAudio.Stop();
        }
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        UnbindGameManagerEvents();

        foreach (Sprite generatedSprite in generatedFreeSpinNumberSprites)
        {
            if (generatedSprite != null)
            {
                Destroy(generatedSprite);
            }
        }

        generatedFreeSpinNumberSprites.Clear();
    }

    #region Scene setup

    private void ResolveSceneReferences()
    {
        SocketManager = SocketManager != null ? SocketManager : FindSceneComponent<SocketIOManager>();
        reelsRoot = reelsRoot != null ? reelsRoot : FindSceneTransform("Slots");

        SlotStart_Button = SlotStart_Button != null ? SlotStart_Button : FindNamedComponent<Button>("Spin");
        StopSpin_Button = StopSpin_Button != null ? StopSpin_Button : FindNamedComponent<Button>("Stop");
        AutoSpinStop_Button = AutoSpinStop_Button != null ? AutoSpinStop_Button : FindNamedComponent<Button>("AutoplayStop");
        TBetPlus_Button = TBetPlus_Button != null ? TBetPlus_Button : FindNamedComponent<Button>("BetIncrease");
        TBetMinus_Button = TBetMinus_Button != null ? TBetMinus_Button : FindNamedComponent<Button>("BetDecrease");

        NormalSpeed_Button = NormalSpeed_Button != null ? NormalSpeed_Button : FindNamedComponent<Button>("NormalSpinSpeed");
        FastSpeed_Button = FastSpeed_Button != null ? FastSpeed_Button : FindNamedComponent<Button>("FastSpinSpeed");
        SkipSpeed_Button = SkipSpeed_Button != null ? SkipSpeed_Button : FindNamedComponent<Button>("SkipSpinSpeed");

        AutoPlayPanel = AutoPlayPanel != null ? AutoPlayPanel : FindSceneObject("Autoplay Panel");
        AutoPlayCount_Text = AutoPlayCount_Text != null ? AutoPlayCount_Text : FindNamedText("AutoplayCount");

        Balance_text = Balance_text != null ? Balance_text : FindNamedText("BalanceAmount");
        TotalBet_text = TotalBet_text != null ? TotalBet_text : FindNamedText("BetAmount");
        TotalWin_text = TotalWin_text != null ? TotalWin_text : FindNamedText("WinAmount", "TotalWin");
        WinLinesCount_Text = WinLinesCount_Text != null ? WinLinesCount_Text : FindNamedText("WinLinesCount");

        FreeSpinPanel = FreeSpinPanel != null ? FreeSpinPanel : FindSceneObject("FreeSpinPanel");
        FreeSpinStart_Button = FreeSpinStart_Button != null
            ? FreeSpinStart_Button
            : FindDescendantComponent<Button>(FreeSpinPanel != null ? FreeSpinPanel.transform : null, "Start");
        FreeSpinCountPanel = FreeSpinCountPanel != null ? FreeSpinCountPanel : FindSceneObject("FreeSpinCountPanel");
        FreeSpinCounter_Image = FreeSpinCounter_Image != null
            ? FreeSpinCounter_Image
            : FindDescendantComponent<Image>(FreeSpinCountPanel != null ? FreeSpinCountPanel.transform : null, "FreeSpinCounter");
        FreeSpinTotal_Image = FreeSpinTotal_Image != null
            ? FreeSpinTotal_Image
            : FindDescendantComponent<Image>(FreeSpinCountPanel != null ? FreeSpinCountPanel.transform : null, "6Text");
        FreeSpinWinPanel = FreeSpinWinPanel != null ? FreeSpinWinPanel : FindSceneObject("FreeSpinWinPanel");
        Take_Button = Take_Button != null ? Take_Button : FindNamedComponent<Button>("Take");

        CacheFreeSpinNumberSprites();
    }

    private void BuildReelCache()
    {
        reels.Clear();

        if (reelsRoot == null)
        {
            Debug.LogError("[SlotBehaviour] The Slots reel root was not found.");
            return;
        }

        for (int reelIndex = 0; reelIndex < reelsRoot.childCount; reelIndex++)
        {
            RectTransform reelTransform = reelsRoot.GetChild(reelIndex) as RectTransform;
            if (reelTransform == null)
            {
                continue;
            }

            ReelRuntime reel = new ReelRuntime
            {
                transform = reelTransform,
                restingPosition = reelTransform.anchoredPosition,
                symbolPitch = CalculateSymbolPitch(reelTransform)
            };

            for (int symbolIndex = 0; symbolIndex < reelTransform.childCount; symbolIndex++)
            {
                Transform symbolTransform = reelTransform.GetChild(symbolIndex);
                Image symbolImage = symbolTransform.GetComponent<Image>();
                if (symbolImage == null)
                {
                    symbolImage = symbolTransform.GetComponentInChildren<Image>(true);
                }

                if (symbolImage != null)
                {
                    reel.symbols.Add(symbolImage);
                }
            }

            if (reel.symbols.Count > 0)
            {
                reels.Add(reel);
            }
        }

        if (reels.Count == 0)
        {
            Debug.LogError("[SlotBehaviour] No reel strips with symbol Images were found.");
            return;
        }

        if (myImages == null || myImages.Length == 0)
        {
            myImages = reels[0].symbols.Select(symbol => symbol.sprite).ToArray();
        }

        images = reels
            .Select(reel => new SlotImage { slotImages = new List<Image>(reel.symbols) })
            .ToList();

        Slot_Transform = reels.Select(reel => (Transform)reel.transform).ToArray();
        Slot_Elements = reels
            .Select(reel => reel.transform.GetComponent<LayoutElement>())
            .Where(element => element != null)
            .ToArray();

        RefreshVisibleImageCache(DefaultRowCount);
    }

    private static float CalculateSymbolPitch(RectTransform reelTransform)
    {
        float height = 200f;
        float spacing = 0f;

        if (reelTransform.childCount > 0 && reelTransform.GetChild(0) is RectTransform firstSymbol)
        {
            height = Mathf.Max(1f, firstSymbol.rect.height);
        }

        VerticalLayoutGroup layout = reelTransform.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            spacing = layout.spacing;
        }

        return Mathf.Max(1f, height + spacing);
    }

    private void RefreshVisibleImageCache(int rowCount)
    {
        Tempimages.Clear();
        int safeRows = Mathf.Max(1, rowCount);

        foreach (ReelRuntime reel in reels)
        {
            int visibleStart = Mathf.Max(0, reel.symbols.Count - safeRows);
            Tempimages.Add(new SlotImage
            {
                slotImages = reel.symbols.Skip(visibleStart).Take(safeRows).ToList()
            });
        }
    }

    private void BindButtons()
    {
        ConfigureSpinButtonHold();
        BindButton(StopSpin_Button, RequestStopSpin);
        BindButton(AutoSpinStop_Button, StopAutoSpin);
        BindButton(TBetPlus_Button, () => ChangeBet(true));
        BindButton(TBetMinus_Button, () => ChangeBet(false));
        BindButton(FreeSpinStart_Button, StartFirstFreeSpin);
        BindButton(Take_Button, TakeFreeSpinWin);

        BindButton(NormalSpeed_Button, () => SetSpeed(SpinSpeed.Turbo));
        BindButton(FastSpeed_Button, () => SetSpeed(SpinSpeed.QuickSpin));
        BindButton(SkipSpeed_Button, () => SetSpeed(SpinSpeed.Normal));

        BindAutoplayChoice("10", 10);
        BindAutoplayChoice("50", 50);
        BindAutoplayChoice("100", 100);
        BindAutoplayChoice("200", 200);
        BindAutoplayChoice("500", 500);
        BindAutoplayChoice("Infinity", InfiniteAutoplay);

    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void BindAutoplayChoice(string objectName, int spinCount)
    {
        Button button = AutoPlayPanel != null
            ? AutoPlayPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.name == objectName)
            : null;

        button = button != null ? button : FindNamedComponent<Button>(objectName);
        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(() => StartAutoSpin(spinCount));
    }

    private void ConfigureSpinButtonHold()
    {
        if (SlotStart_Button == null)
        {
            return;
        }

        SlotStart_Button.onClick.RemoveListener(StartSlots);

        EventTrigger eventTrigger = SlotStart_Button.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = SlotStart_Button.gameObject.AddComponent<EventTrigger>();
        }

        AddSpinButtonPointerEvent(eventTrigger, EventTriggerType.PointerDown, OnSpinButtonPointerDown);
        AddSpinButtonPointerEvent(eventTrigger, EventTriggerType.PointerUp, OnSpinButtonPointerUp);
        AddSpinButtonPointerEvent(eventTrigger, EventTriggerType.PointerExit, OnSpinButtonPointerExit);
    }

    private static void AddSpinButtonPointerEvent(
        EventTrigger eventTrigger,
        EventTriggerType eventType,
        UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };

        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
    }

    private void OnSpinButtonPointerDown(BaseEventData eventData)
    {
        if (isSpinButtonPointerDown || SlotStart_Button == null || !SlotStart_Button.interactable)
        {
            return;
        }

        isSpinButtonPointerDown = true;
        spinButtonHoldTriggered = false;
        spinButtonHoldRoutine = StartCoroutine(SpinButtonHoldCheckRoutine());
    }

    private void OnSpinButtonPointerUp(BaseEventData eventData)
    {
        if (!isSpinButtonPointerDown)
        {
            return;
        }

        isSpinButtonPointerDown = false;
        StopSpinButtonHoldRoutine();

        if (!spinButtonHoldTriggered && SlotStart_Button != null && SlotStart_Button.interactable)
        {
            StartSlots();
        }
    }

    private void OnSpinButtonPointerExit(BaseEventData eventData)
    {
        if (!isSpinButtonPointerDown)
        {
            return;
        }

        CancelSpinButtonHold();
    }

    private IEnumerator SpinButtonHoldCheckRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, spinButtonHoldDuration));

        if (isSpinButtonPointerDown && SlotStart_Button != null && SlotStart_Button.interactable)
        {
            spinButtonHoldTriggered = true;
            OpenAutoplayPanelFromHold();
        }

        spinButtonHoldRoutine = null;
    }

    private void CancelSpinButtonHold()
    {
        isSpinButtonPointerDown = false;
        spinButtonHoldTriggered = false;
        StopSpinButtonHoldRoutine();
    }

    private void StopSpinButtonHoldRoutine()
    {
        if (spinButtonHoldRoutine == null)
        {
            return;
        }

        StopCoroutine(spinButtonHoldRoutine);
        spinButtonHoldRoutine = null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        GameObject sceneObject = FindSceneObject(objectName);
        return sceneObject != null ? sceneObject.transform : null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        foreach (T candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static T FindNamedComponent<T>(string objectName) where T : Component
    {
        GameObject sceneObject = FindSceneObject(objectName);
        if (sceneObject == null)
        {
            return null;
        }

        T component = sceneObject.GetComponent<T>();
        return component != null ? component : sceneObject.GetComponentInChildren<T>(true);
    }

    private static TMP_Text FindNamedText(params string[] objectNames)
    {
        foreach (string objectName in objectNames)
        {
            TMP_Text text = FindNamedComponent<TMP_Text>(objectName);
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private static T FindDescendantComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] candidates = root.GetComponentsInChildren<T>(true);
        return candidates.FirstOrDefault(candidate => candidate != null && candidate.name == objectName);
    }

    #endregion

    #region Game state integration

    private void BindGameManagerEvents()
    {
        if (gameManagerEventsBound)
        {
            return;
        }

        gameManager = gameManager != null ? gameManager : FindSceneComponent<GameManager>();
        if (gameManager == null)
        {
            return;
        }

        gameManager.InitDataReceived += ApplyInitialization;
        gameManager.SpinResultReceived += ApplySpinResult;
        gameManager.Disconnected += OnGameDisconnected;
        gameManagerEventsBound = true;
    }

    private void UnbindGameManagerEvents()
    {
        if (!gameManagerEventsBound || gameManager == null)
        {
            return;
        }

        gameManager.InitDataReceived -= ApplyInitialization;
        gameManager.SpinResultReceived -= ApplySpinResult;
        gameManager.Disconnected -= OnGameDisconnected;
        gameManagerEventsBound = false;
    }

    private void OnGameDisconnected()
    {
        socketConnected = false;
        if (IsSpinning)
        {
            resultFailed = true;
        }
    }

    private void ApplyInitialization(
        GameConfig config,
        PlayerData initialPlayerData,
        List<List<int>> initialMatrix)
    {
        if (config == null || initialPlayerData == null)
        {
            Debug.LogError("[SlotBehaviour] Initialization data is incomplete.");
            return;
        }

        try
        {
            gameConfig = config;
            playerData = initialPlayerData;
            BetCounter = Mathf.Clamp(playerData.currentBetIndex, 0, Math.Max(0, gameConfig.availableBets.Count - 1));
            currentBalance = playerData.balance;
            isInitialized = true;
            socketConnected = true;
            waitingForLateResult = false;

            CopyStateToGameManager();
            RefreshVisibleImageCache(gameConfig.rowCount);
            ApplyMatrix(IsValidMatrix(initialMatrix) ? initialMatrix : GenerateRandomMatrix(gameConfig.rowCount));
            RefreshBetValues();
            UpdateBalanceText(currentBalance);
            SetWinAmount(0d, false);
            UpdateWinLineCount(gameConfig.paylineCount);
            UpdateFreeSpinDisplay();
            SetControlState();
        }
        catch (Exception exception)
        {
            isInitialized = false;
            Debug.LogError($"[SlotBehaviour] Initialization failed: {exception.Message}");
        }
    }

    private void ApplySpinResult(SpinResult result)
    {
        if (result == null)
        {
            resultFailed = true;
            return;
        }

        if (!IsSpinning && !waitingForLateResult)
        {
            Debug.LogWarning("[SlotBehaviour] Ignored an unexpected spin result because no spin is pending.");
            return;
        }

        if (result.playerData != null)
        {
            result.playerData.currentBetIndex = BetCounter;
        }

        ApplySantaFeatureSymbols(result);

        if (waitingForLateResult && !IsSpinning)
        {
            waitingForLateResult = false;
            PresentResult(result);
            SetControlState();
            return;
        }

        pendingResult = result;
        resultReceived = true;
    }

    private void ApplySantaFeatureSymbols(SpinResult result)
    {
        if (result?.resultMatrix == null || gameConfig == null)
        {
            return;
        }

        if (result.expandedWildReels != null)
        {
            foreach (int reelIndex in result.expandedWildReels)
            {
                if (reelIndex < 0 || reelIndex >= result.resultMatrix.Count || result.resultMatrix[reelIndex] == null)
                {
                    continue;
                }

                for (int row = 0; row < result.resultMatrix[reelIndex].Count; row++)
                {
                    result.resultMatrix[reelIndex][row] = gameConfig.expandingWildSymbolId;
                }
            }
        }

        if (result.extraGiftWilds == null)
        {
            return;
        }

        foreach (ServerExtraGiftWild giftWild in result.extraGiftWilds)
        {
            ServerPosition position = giftWild?.position;
            if (position == null || position.col < 0 || position.col >= result.resultMatrix.Count ||
                result.resultMatrix[position.col] == null || position.row < 0 ||
                position.row >= result.resultMatrix[position.col].Count)
            {
                continue;
            }

            result.resultMatrix[position.col][position.row] = gameConfig.giftWildSymbolId;
        }
    }

    private void CopyStateToGameManager()
    {
        gameManager = gameManager != null ? gameManager : FindSceneComponent<GameManager>();
        if (gameManager == null)
        {
            return;
        }

        gameManager.gameConfig = gameConfig;
        gameManager.playerData = playerData;
        gameManager.currentBetIndex = BetCounter;
        gameManager.currentBetAmount = currentLineBet;
        gameManager.isInitialized = isInitialized;
        gameManager.initializationFailed = !isInitialized;
    }

    #endregion

    #region Spin flow

    public void StartSlots()
    {
        TryStartSpin(false);
    }

    private bool TryStartSpin(bool continuousSpin)
    {
        if (IsSpinning || waitingForLateResult || waitingForFreeSpinStart || waitingForFreeSpinTake || !CanSendSpin())
        {
            return false;
        }

        if (!IsFreeSpin && currentBalance + 0.0000001d < currentTotalBet)
        {
            StopAutoSpin();
            onInsufficientBalance?.Invoke();
            Debug.LogWarning("[SlotBehaviour] Spin blocked because the balance is below the selected total bet.");
            return false;
        }

        StopWinningAnimations();
        SetWinAmount(0d, false);

        IsSpinning = true;
        stopSpinRequested = spinSpeed == SpinSpeed.QuickSpin;
        resultReceived = false;
        resultFailed = false;
        pendingResult = null;
        CheckPopups = false;

        if (!IsFreeSpin)
        {
            AnimateBalanceTo(Math.Max(0d, currentBalance - currentTotalBet));
        }

        SetControlState();
        PlayAudio(buttonAudio);
        PlayLoopingAudio(spinAudio);
        onSpinStarted?.Invoke();

        SocketManager.SendSpinRequest(BetCounter, IsFreeSpin);
        spinRoutine = StartCoroutine(SpinCoroutine(continuousSpin));
        return true;
    }

    private bool CanSendSpin()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[SlotBehaviour] Spin ignored until game initialization is complete.");
            return false;
        }

        if (SocketManager == null || !SocketManager.isConnected)
        {
            Debug.LogWarning("[SlotBehaviour] Spin ignored because the socket is not connected.");
            return false;
        }

        if (gameConfig?.availableBets == null || gameConfig.availableBets.Count == 0)
        {
            Debug.LogError("[SlotBehaviour] Spin ignored because no bet options were supplied by the server.");
            return false;
        }

        if (reels.Count == 0 || myImages == null || myImages.Length == 0)
        {
            Debug.LogError("[SlotBehaviour] Spin ignored because the reel visuals are not ready.");
            return false;
        }

        return true;
    }

    private IEnumerator SpinCoroutine(bool continuousSpin)
    {
        float startTime = Time.realtimeSinceStartup;
        float minimumSpinTime = GetMinimumSpinTime();

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            StartReelMotion(reels[reelIndex]);
            if (spinSpeed == SpinSpeed.Normal && reelIndex < reels.Count - 1)
            {
                yield return new WaitForSecondsRealtime(reelStartStagger);
            }
        }

        while (!resultReceived && !resultFailed)
        {
            if (Time.realtimeSinceStartup - startTime >= resultTimeout)
            {
                waitingForLateResult = true;
                yield return AbortSpin("Timed out while waiting for a server result.");
                yield break;
            }

            yield return null;
        }

        if (resultFailed || pendingResult == null)
        {
            yield return AbortSpin("The spin request failed before a valid result was received.");
            yield break;
        }

        if (!stopSpinRequested)
        {
            float remainingMinimumTime = minimumSpinTime - (Time.realtimeSinceStartup - startTime);
            if (remainingMinimumTime > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingMinimumTime);
            }

            if (spinSpeed == SpinSpeed.Normal)
            {
                yield return new WaitUntil(
                    () => stopSpinRequested || reels.All(reel => reel.completedCycles >= minSpinCyclesBeforeStop));
            }
        }

        StopLoopingAudio(spinAudio);
        yield return StopReelsAndApplyMatrix(pendingResult.resultMatrix);

        SpinResult completedResult = pendingResult;
        pendingResult = null;
        resultReceived = false;
        spinRoutine = null;

        PresentResult(completedResult);
        IsSpinning = false;
        SetControlState();
    }

    private IEnumerator AbortSpin(string reason)
    {
        Debug.LogError($"[SlotBehaviour] {reason}");
        StopLoopingAudio(spinAudio);
        KillReelTweens(true);
        balanceTween?.Kill();
        balanceTween = null;
        UpdateBalanceText(currentBalance);

        IsSpinning = false;
        resultReceived = false;
        resultFailed = false;
        pendingResult = null;
        spinRoutine = null;
        StopAutoSpin();
        onSpinRequestFailed?.Invoke();
        SetControlState();
        yield return null;
    }

    private void StartReelMotion(ReelRuntime reel)
    {
        reel.motionTween?.Kill();
        reel.transform.anchoredPosition = reel.restingPosition;
        reel.completedCycles = 0;

        int visibleRows = GetRowCount();
        int travelSymbols = Mathf.Max(3, reel.symbols.Count - visibleRows);
        float travelDistance = reel.symbolPitch * travelSymbols;
        float pixelsPerSecond = spinSpeed == SpinSpeed.Normal ? normalReelSpeed : fastReelSpeed;
        float duration = Mathf.Max(0.08f, travelDistance / pixelsPerSecond);

        reel.motionTween = reel.transform
            .DOAnchorPosY(reel.restingPosition.y - travelDistance, duration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .OnStepComplete(() => reel.completedCycles++)
            .SetUpdate(true);

        activeTweens.Add(reel.motionTween);
    }

    private IEnumerator StopReelsAndApplyMatrix(List<List<int>> resultMatrix)
    {
        if (!IsValidMatrix(resultMatrix))
        {
            Debug.LogError("[SlotBehaviour] The server result matrix does not match the visible reels.");
            KillReelTweens(true);
            yield break;
        }

        bool quickStop = stopSpinRequested || spinSpeed == SpinSpeed.QuickSpin;
        bool turboStop = !quickStop && spinSpeed == SpinSpeed.Turbo;
        float timingScale = quickStop ? 0.35f : turboStop ? 0.55f : 1f;
        float stopStagger = quickStop ? reelStopStagger * 0.25f : turboStop ? reelStopStagger * 0.5f : reelStopStagger;
        float overshoot = stopOvershootDistance * timingScale;
        float overshootDuration = stopOvershootDuration * timingScale;
        float settleDuration = stopSettleDuration * timingScale;

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            StartCoroutine(StopSingleReel(
                reelIndex,
                resultMatrix[reelIndex],
                reelIndex * stopStagger,
                quickStop,
                overshoot,
                overshootDuration,
                settleDuration));
        }

        float completeAfter = Math.Max(0, reels.Count - 1) * stopStagger + overshootDuration + settleDuration;
        if (completeAfter > 0f)
        {
            yield return new WaitForSecondsRealtime(completeAfter);
        }

        foreach (ReelRuntime reel in reels)
        {
            reel.transform.anchoredPosition = reel.restingPosition;
        }

        activeTweens.RemoveAll(tween => tween == null || !tween.IsActive());
    }

    private IEnumerator StopSingleReel(
        int reelIndex,
        List<int> resultColumn,
        float delay,
        bool quickStop,
        float overshoot,
        float overshootDuration,
        float settleDuration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        ReelRuntime reel = reels[reelIndex];
        reel.motionTween?.Kill();
        reel.motionTween = null;
        ApplyMatrixColumn(reelIndex, resultColumn);

        float landingDistance = Mathf.Max(stopAnticipationDistance, reel.symbolPitch * (quickStop ? 0.75f : 2f));
        reel.transform.anchoredPosition = reel.restingPosition + Vector2.up * landingDistance;

        Sequence stopSequence = DOTween.Sequence().SetUpdate(true);
        stopSequence.Append(
            reel.transform
                .DOAnchorPosY(reel.restingPosition.y - overshoot, overshootDuration)
                .SetEase(Ease.OutQuad));
        stopSequence.Append(
            reel.transform
                .DOAnchorPos(reel.restingPosition, settleDuration)
                .SetEase(Ease.InOutQuad));

        activeTweens.Add(stopSequence);
        yield return stopSequence.WaitForCompletion();
    }

    private void ApplyMatrix(List<List<int>> matrix)
    {
        if (!IsValidMatrix(matrix))
        {
            return;
        }

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ApplyMatrixColumn(reelIndex, matrix[reelIndex]);
            reels[reelIndex].transform.anchoredPosition = reels[reelIndex].restingPosition;
        }
    }

    private void ApplyMatrixColumn(int reelIndex, List<int> column)
    {
        ReelRuntime reel = reels[reelIndex];
        int rowCount = Mathf.Min(GetRowCount(), column.Count);
        int visibleStart = Mathf.Max(0, reel.symbols.Count - rowCount);

        for (int row = 0; row < rowCount; row++)
        {
            int symbolId = column[row];
            if (symbolId < 0 || symbolId >= myImages.Length || myImages[symbolId] == null)
            {
                Debug.LogWarning($"[SlotBehaviour] Invalid symbol id {symbolId} at reel {reelIndex}, row {row}.");
                continue;
            }

            reel.symbols[visibleStart + row].sprite = myImages[symbolId];
        }
    }

    private bool IsValidMatrix(List<List<int>> matrix)
    {
        if (matrix == null || matrix.Count < reels.Count)
        {
            return false;
        }

        int rowCount = GetRowCount();
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            if (matrix[reelIndex] == null || matrix[reelIndex].Count < rowCount)
            {
                return false;
            }
        }

        return true;
    }

    private void PresentResult(SpinResult result)
    {
        if (result == null)
        {
            return;
        }

        ApplyMatrix(result.resultMatrix);

        if (result.playerData != null)
        {
            playerData = result.playerData;
            currentBalance = result.playerData.balance;
            UpdateBalanceText(currentBalance);
        }

        double displayedWin = result.grandTotalWin > 0d ? result.grandTotalWin : result.winAmount;
        SetWinAmount(displayedWin, true);

        if (displayedWin > 0d)
        {
            PlayAudio(winAudio);
            CheckWinPopups(result);
        }

        StartResultAnimations(result);

        UpdateFreeSpinState(result);

        CopyStateToGameManager();
        onSpinStopped?.Invoke(result);
    }

    private void RequestStopSpin()
    {
        if (!IsSpinning)
        {
            return;
        }

        PlayAudio(buttonAudio);
        stopSpinRequested = true;
        if (StopSpin_Button != null)
        {
            StopSpin_Button.gameObject.SetActive(false);
        }
    }

    private float GetMinimumSpinTime()
    {
        switch (spinSpeed)
        {
            case SpinSpeed.Turbo:
                return fastMinimumSpinTime;
            case SpinSpeed.QuickSpin:
                return 0f;
            default:
                return normalMinimumSpinTime;
        }
    }

    #endregion

    #region Autoplay and free games

    public void AutoSpin()
    {
        ToggleAutoplayPanel();
    }

    private void ToggleAutoplayPanel()
    {
        if (IsSpinning || IsFreeSpin || waitingForFreeSpinStart || waitingForFreeSpinTake || AutoPlayPanel == null)
        {
            return;
        }

        PlayAudio(buttonAudio);
        AutoPlayPanel.SetActive(!AutoPlayPanel.activeSelf);
    }

    public void StartAutoSpin(int spinCount)
    {
        if (IsAutoSpin || spinCount == 0)
        {
            return;
        }

        if (!isInitialized || waitingForLateResult || waitingForFreeSpinStart || waitingForFreeSpinTake)
        {
            return;
        }

        autoplaySpinsRemaining = spinCount < 0 ? InfiniteAutoplay : spinCount;
        IsAutoSpin = true;
        WasAutoSpinOn = false;

        if (AutoPlayPanel != null)
        {
            AutoPlayPanel.SetActive(false);
        }

        UpdateAutoplayDisplay();
        SetControlState();

        if (autoSpinRoutine != null)
        {
            StopCoroutine(autoSpinRoutine);
        }

        autoSpinRoutine = StartCoroutine(AutoSpinCoroutine());
    }

    public void StopAutoSpin()
    {
        if (!IsAutoSpin && autoSpinRoutine == null)
        {
            return;
        }

        IsAutoSpin = false;
        autoplaySpinsRemaining = 0;
        UpdateAutoplayDisplay();
        SetControlState();
    }

    private IEnumerator AutoSpinCoroutine()
    {
        while (IsAutoSpin)
        {
            if (IsFreeSpin || IsSpinning || waitingForFreeSpinStart || waitingForFreeSpinTake)
            {
                yield return null;
                continue;
            }

            if (autoplaySpinsRemaining == 0)
            {
                break;
            }

            bool started = TryStartSpin(true);
            if (!started)
            {
                break;
            }

            if (autoplaySpinsRemaining > 0)
            {
                autoplaySpinsRemaining--;
                UpdateAutoplayDisplay();
            }

            yield return new WaitUntil(() => !IsSpinning || !IsAutoSpin);

            if (!IsAutoSpin)
            {
                break;
            }

            float delay = TotalWinValue() > 0d ? autoSpinWinGap : autoSpinGap;
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        IsAutoSpin = false;
        autoSpinRoutine = null;
        UpdateAutoplayDisplay();
        SetControlState();
    }

    internal void FreeSpin(int spins)
    {
        if (spins <= 0)
        {
            return;
        }

        BeginFreeSpins(spins, 0, spins, 0d, null);
    }

    private void UpdateFreeSpinState(SpinResult result)
    {
        bool hasFreeGameTrigger = result.freeSpinData != null && result.freeSpinData.isTriggered;
        bool hasFreeGameRoundState = hasFreeGameTrigger || result.serverTotalSpins > 0;
        int reportedRemaining = result.serverSpinsRemaining;

        if (hasFreeGameTrigger && reportedRemaining <= 0)
        {
            reportedRemaining = Math.Max(result.freeSpinData.remainingSpins, result.freeSpinData.spinsAwarded);
        }

        if (hasFreeGameTrigger && !IsFreeSpin && !waitingForFreeSpinStart)
        {
            int totalSpins = result.serverTotalSpins > 0
                ? result.serverTotalSpins
                : Math.Max(result.freeSpinData.spinsAwarded, reportedRemaining);
            BeginFreeSpins(
                reportedRemaining,
                Math.Max(0, result.serverSpinsUsed),
                totalSpins,
                result.serverTotalRoundWin,
                result);
            return;
        }

        // Santa Riches reports whether the completed spin was a free spin but
        // keeps the remaining count on the client. Retriggers add their award
        // before the completed spin is deducted.
        if (IsFreeSpin && result.isFreeSpinResult)
        {
            if (hasFreeGameTrigger && result.freeSpinData.spinsAwarded > 0)
            {
                int retriggerAward = result.freeSpinData.spinsAwarded;
                freeSpinsRemaining += retriggerAward;
                freeSpinsTotal += retriggerAward;
                onFreeSpinsTriggered?.Invoke(result);
            }

            freeSpinsRemaining = Math.Max(0, freeSpinsRemaining - 1);
            freeSpinsUsed++;
            freeSpinTotalWin += Math.Max(0d, result.winAmount);

            if (freeSpinsRemaining <= 0)
            {
                CompleteFreeSpins();
            }
            else
            {
                UpdateFreeSpinDisplay();
            }

            return;
        }

        if (IsFreeSpin && result.isRoundOver)
        {
            freeSpinsRemaining = 0;
            freeSpinsUsed = Math.Max(freeSpinsUsed, result.serverSpinsUsed);
            freeSpinTotalWin = Math.Max(freeSpinTotalWin, result.serverTotalRoundWin);
            CompleteFreeSpins();
            return;
        }

        if (!IsFreeSpin || !hasFreeGameRoundState)
        {
            return;
        }

        freeSpinsRemaining = Math.Max(0, reportedRemaining);
        freeSpinsUsed = Math.Max(0, result.serverSpinsUsed);
        if (result.serverTotalSpins > 0)
        {
            freeSpinsTotal = result.serverTotalSpins;
        }

        freeSpinTotalWin = Math.Max(freeSpinTotalWin, result.serverTotalRoundWin);

        if (result.isRoundOver || reportedRemaining <= 0)
        {
            CompleteFreeSpins();
            return;
        }

        UpdateFreeSpinDisplay();
    }

    private void BeginFreeSpins(
        int remaining,
        int used,
        int total,
        double totalWin,
        SpinResult triggerResult)
    {
        WasAutoSpinOn = IsAutoSpin;
        IsFreeSpin = true;
        waitingForFreeSpinStart = true;
        waitingForFreeSpinTake = false;
        freeSpinsRemaining = Math.Max(0, remaining);
        freeSpinsUsed = Math.Max(0, used);
        freeSpinsTotal = Math.Max(1, total);
        freeSpinTotalWin = Math.Max(0d, totalWin);

        if (triggerResult != null)
        {
            onFreeSpinsTriggered?.Invoke(triggerResult);
        }

        UpdateFreeSpinDisplay();
        SetControlState();
    }

    private void StartFirstFreeSpin()
    {
        if (!waitingForFreeSpinStart || !IsFreeSpin || freeSpinsRemaining <= 0)
        {
            return;
        }

        PlayAudio(buttonAudio);
        waitingForFreeSpinStart = false;
        UpdateFreeSpinDisplay();
        SetControlState();

        if (freeSpinRoutine == null)
        {
            freeSpinRoutine = StartCoroutine(FreeSpinCoroutine());
        }
    }

    private void CompleteFreeSpins()
    {
        IsFreeSpin = false;
        waitingForFreeSpinStart = false;
        waitingForFreeSpinTake = true;
        freeSpinsRemaining = 0;

        if (freeSpinTotalWin > 0d)
        {
            SetWinAmount(freeSpinTotalWin, true);
        }

        UpdateFreeSpinDisplay();
        SetControlState();
    }

    private void TakeFreeSpinWin()
    {
        if (!waitingForFreeSpinTake)
        {
            return;
        }

        PlayAudio(buttonAudio);
        waitingForFreeSpinTake = false;
        WasAutoSpinOn = false;
        freeSpinsUsed = 0;
        freeSpinsTotal = 0;
        freeSpinTotalWin = 0d;
        UpdateFreeSpinDisplay();
        SetControlState();
    }

    private IEnumerator FreeSpinCoroutine()
    {
        yield return new WaitUntil(() => !IsSpinning);
        yield return new WaitForSecondsRealtime(0.75f);

        while (IsFreeSpin && freeSpinsRemaining > 0)
        {
            if (!TryStartSpin(true))
            {
                if (waitingForLateResult)
                {
                    yield return new WaitUntil(() => !waitingForLateResult || shuttingDown);
                    if (!shuttingDown)
                    {
                        continue;
                    }
                }

                break;
            }

            yield return new WaitUntil(() => !IsSpinning);

            if (IsFreeSpin && freeSpinsRemaining > 0)
            {
                yield return new WaitForSecondsRealtime(autoSpinGap);
            }
        }

        if (freeSpinsRemaining <= 0 && !waitingForFreeSpinTake)
        {
            IsFreeSpin = false;
            freeSpinsRemaining = 0;
        }

        freeSpinRoutine = null;
        UpdateFreeSpinDisplay();
        SetControlState();
    }

    private void OpenAutoplayPanelFromHold()
    {
        if (AutoPlayPanel == null || IsSpinning || IsAutoSpin || IsFreeSpin || waitingForLateResult ||
            waitingForFreeSpinStart || waitingForFreeSpinTake)
        {
            return;
        }

        AutoPlayPanel.SetActive(true);
        PlayAudio(buttonAudio);
    }

    #endregion

    #region Bet and speed controls

    private void ChangeBet(bool increase)
    {
        if (IsSpinning || IsAutoSpin || IsFreeSpin || waitingForFreeSpinStart || waitingForFreeSpinTake ||
            gameConfig?.availableBets == null || gameConfig.availableBets.Count == 0)
        {
            return;
        }

        PlayAudio(buttonAudio);
        int direction = increase ? 1 : -1;
        BetCounter = (BetCounter + direction + gameConfig.availableBets.Count) % gameConfig.availableBets.Count;
        RefreshBetValues();
    }

    private void RefreshBetValues()
    {
        if (gameConfig?.availableBets == null || gameConfig.availableBets.Count == 0)
        {
            currentLineBet = 0d;
            currentTotalBet = 0d;
            return;
        }

        BetCounter = Mathf.Clamp(BetCounter, 0, gameConfig.availableBets.Count - 1);
        currentLineBet = gameConfig.availableBets[BetCounter];
        double divisor = gameConfig.creditDivisor > 0d ? gameConfig.creditDivisor : 1d;
        currentTotalBet = currentLineBet * divisor;

        if (LineBet_text != null)
        {
            LineBet_text.text = FormatAmount(currentLineBet);
        }

        if (TotalBet_text != null)
        {
            TotalBet_text.text = FormatAmount(currentTotalBet);
        }

        CopyStateToGameManager();
    }

    private void TurboToggle()
    {
        SpinSpeed nextSpeed = spinSpeed == SpinSpeed.Normal
            ? SpinSpeed.Turbo
            : spinSpeed == SpinSpeed.Turbo
                ? SpinSpeed.QuickSpin
                : SpinSpeed.Normal;

        SetSpeed(nextSpeed);
    }

    private void SetSpeed(SpinSpeed speed, bool playSound = true)
    {
        if (IsSpinning)
        {
            return;
        }

        spinSpeed = speed;
        if (playSound)
        {
            PlayAudio(buttonAudio);
        }

        if (NormalSpeed_Button != null)
        {
            NormalSpeed_Button.gameObject.SetActive(spinSpeed == SpinSpeed.Normal);
        }

        if (FastSpeed_Button != null)
        {
            FastSpeed_Button.gameObject.SetActive(spinSpeed == SpinSpeed.Turbo);
        }

        if (SkipSpeed_Button != null)
        {
            SkipSpeed_Button.gameObject.SetActive(spinSpeed == SpinSpeed.QuickSpin);
        }
    }

    #endregion

    #region Results, wins and compatibility hooks

    internal void UpdateBalanceDisplay(double newBalance)
    {
        currentBalance = Math.Max(0d, newBalance);
        if (playerData == null)
        {
            playerData = new PlayerData();
        }

        playerData.balance = currentBalance;
        UpdateBalanceText(currentBalance);
    }

    internal void InitializeMatrix()
    {
        int rowCount = GetRowCount();
        ApplyMatrix(GenerateRandomMatrix(rowCount));
    }

    internal void SetInitialUI()
    {
        RefreshBetValues();
        UpdateBalanceText(currentBalance);
        SetWinAmount(0d, false);
        UpdateWinLineCount(gameConfig != null ? gameConfig.paylineCount : 0);
        UpdateFreeSpinDisplay();
        SetControlState();
    }

    internal void CheckWinPopups()
    {
        if (pendingResult != null)
        {
            CheckWinPopups(pendingResult);
        }
    }

    private void CheckWinPopups(SpinResult result)
    {
        double win = result.grandTotalWin > 0d ? result.grandTotalWin : result.winAmount;
        if (win <= 0d || currentTotalBet <= 0d)
        {
            CheckPopups = false;
            return;
        }

        double multiplier = win / currentTotalBet;
        int tier = multiplier >= 15d ? 3 : multiplier >= 10d ? 2 : multiplier >= 5d ? 1 : 0;
        CheckPopups = tier > 0;
        if (tier > 0)
        {
            onWinTier?.Invoke(tier, win);
        }
    }

    internal void CallCloseSocket()
    {
        StopAutoSpin();
        SocketManager?.CloseSocket();
    }

    private void StartResultAnimations(SpinResult result)
    {
        StopWinningAnimations();
        if (result == null)
        {
            return;
        }

        bool loopIndividualLines = !IsAutoSpin && !IsFreeSpin && !result.isFreeSpinResult &&
                                   (result.freeSpinData == null || !result.freeSpinData.isTriggered);
        winAnimationRoutine = StartCoroutine(PlayResultAnimations(result, loopIndividualLines));
    }

    private IEnumerator PlayResultAnimations(SpinResult result, bool loopIndividualLines)
    {
        HashSet<int> featurePositions = GetSantaFeaturePositions(result);
        if (featurePositions.Count > 0)
        {
            yield return PulseSymbolPositions(featurePositions, singleWinLineDuration * 0.75f);
        }

        HashSet<int> allWinPositions = GetWinningPositions(result.winLines);
        if (allWinPositions.Count > 0)
        {
            yield return PulseSymbolPositions(allWinPositions, allWinsDuration);
        }

        if (!loopIndividualLines || result.winLines == null || result.winLines.Count == 0)
        {
            winAnimationRoutine = null;
            yield break;
        }

        while (!shuttingDown && !IsSpinning)
        {
            foreach (WinLine winLine in result.winLines)
            {
                if (shuttingDown || IsSpinning)
                {
                    winAnimationRoutine = null;
                    yield break;
                }

                if (winLine?.positions != null && winLine.positions.Count > 0)
                {
                    yield return PulseSymbolPositions(
                        new HashSet<int>(winLine.positions),
                        singleWinLineDuration);
                }
            }
        }

        winAnimationRoutine = null;
    }

    private HashSet<int> GetSantaFeaturePositions(SpinResult result)
    {
        HashSet<int> positions = new HashSet<int>();
        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : DefaultReelCount;
        int rowCount = GetRowCount();

        if (result.expandedWildReels != null)
        {
            foreach (int reelIndex in result.expandedWildReels)
            {
                if (reelIndex < 0 || reelIndex >= reelCount)
                {
                    continue;
                }

                for (int row = 0; row < rowCount; row++)
                {
                    positions.Add(row * reelCount + reelIndex);
                }
            }
        }

        if (result.extraGiftWilds != null)
        {
            foreach (ServerExtraGiftWild giftWild in result.extraGiftWilds)
            {
                if (giftWild?.position != null)
                {
                    positions.Add(giftWild.position.row * reelCount + giftWild.position.col);
                }
            }
        }

        if (result.scatterData?.positions != null)
        {
            positions.UnionWith(result.scatterData.positions);
        }

        return positions;
    }

    private static HashSet<int> GetWinningPositions(List<WinLine> winLines)
    {
        HashSet<int> positions = new HashSet<int>();
        if (winLines == null)
        {
            return positions;
        }

        foreach (WinLine winLine in winLines)
        {
            if (winLine?.positions != null)
            {
                positions.UnionWith(winLine.positions);
            }
        }

        return positions;
    }

    private IEnumerator PulseSymbolPositions(HashSet<int> positions, float duration)
    {
        float safeDuration = Mathf.Max(0.02f, duration);
        float halfDuration = safeDuration * 0.5f;
        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : DefaultReelCount;
        int rowCount = GetRowCount();

        foreach (int position in positions)
        {
            int row = position / reelCount;
            int reelIndex = position % reelCount;
            if (reelIndex < 0 || reelIndex >= reels.Count || row < 0 || row >= rowCount)
            {
                continue;
            }

            ReelRuntime reel = reels[reelIndex];
            int visibleStart = Mathf.Max(0, reel.symbols.Count - rowCount);
            Image symbol = reel.symbols[visibleStart + row];
            symbol.rectTransform.localScale = Vector3.one;

            Sequence pulse = DOTween.Sequence().SetUpdate(true);
            pulse.Append(symbol.rectTransform.DOScale(winningSymbolScale, halfDuration).SetEase(Ease.OutQuad));
            pulse.Append(symbol.rectTransform.DOScale(1f, halfDuration).SetEase(Ease.InQuad));
            winTweens.Add(pulse);
        }

        yield return new WaitForSecondsRealtime(safeDuration);
        winTweens.RemoveAll(tween => tween == null || !tween.IsActive());
    }

    private void StopWinningAnimations()
    {
        if (winAnimationRoutine != null)
        {
            StopCoroutine(winAnimationRoutine);
            winAnimationRoutine = null;
        }

        foreach (Tween tween in winTweens)
        {
            tween?.Kill();
        }

        winTweens.Clear();

        foreach (ReelRuntime reel in reels)
        {
            foreach (Image symbol in reel.symbols)
            {
                if (symbol != null)
                {
                    symbol.rectTransform.localScale = Vector3.one;
                }
            }
        }
    }

    private void SetWinAmount(double amount, bool animate)
    {
        winAmountTween?.Kill();

        if (TotalWin_text == null)
        {
            return;
        }

        if (!animate || amount <= 0d)
        {
            TotalWin_text.text = FormatAmount(Math.Max(0d, amount));
            return;
        }

        double displayed = 0d;
        winAmountTween = DOTween.To(
                () => displayed,
                value =>
                {
                    displayed = value;
                    if (TotalWin_text != null)
                    {
                        TotalWin_text.text = FormatAmount(displayed);
                    }
                },
                amount,
                0.65f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private double TotalWinValue()
    {
        if (TotalWin_text == null)
        {
            return 0d;
        }

        return double.TryParse(TotalWin_text.text, out double value) ? value : 0d;
    }

    #endregion

    #region UI and cleanup helpers

    private void SetControlState()
    {
        bool normalIdle = !IsSpinning && !IsAutoSpin && !IsFreeSpin && !waitingForLateResult &&
                          !waitingForFreeSpinStart && !waitingForFreeSpinTake;
        bool showManualStop = IsSpinning && !IsAutoSpin && !IsFreeSpin && spinSpeed == SpinSpeed.Normal;

        if (SlotStart_Button != null)
        {
            SlotStart_Button.gameObject.SetActive(normalIdle);
            SlotStart_Button.interactable = normalIdle && isInitialized;
        }

        if (StopSpin_Button != null)
        {
            StopSpin_Button.gameObject.SetActive(showManualStop && !stopSpinRequested);
            StopSpin_Button.interactable = showManualStop;
        }

        if (AutoSpinStop_Button != null)
        {
            bool showAutoplayStop = IsAutoSpin && !IsFreeSpin && !waitingForFreeSpinStart && !waitingForFreeSpinTake;
            AutoSpinStop_Button.gameObject.SetActive(showAutoplayStop);
            AutoSpinStop_Button.interactable = showAutoplayStop;
        }

        if (FreeSpinStart_Button != null)
        {
            FreeSpinStart_Button.interactable = waitingForFreeSpinStart;
        }

        if (Take_Button != null)
        {
            Take_Button.gameObject.SetActive(waitingForFreeSpinTake);
            Take_Button.interactable = waitingForFreeSpinTake;
        }

        ToggleButtonGrp(normalIdle);
    }

    private void ToggleButtonGrp(bool toggle)
    {
        if (TBetMinus_Button != null) TBetMinus_Button.interactable = toggle;
        if (TBetPlus_Button != null) TBetPlus_Button.interactable = toggle;
        if (NormalSpeed_Button != null) NormalSpeed_Button.interactable = toggle;
        if (FastSpeed_Button != null) FastSpeed_Button.interactable = toggle;
        if (SkipSpeed_Button != null) SkipSpeed_Button.interactable = toggle;
    }

    private void UpdateAutoplayDisplay()
    {
        if (AutoPlayCount_Text == null)
        {
            return;
        }

        bool visible = IsAutoSpin && !IsFreeSpin && !waitingForFreeSpinStart && !waitingForFreeSpinTake;
        AutoPlayCount_Text.gameObject.SetActive(visible);
        AutoPlayCount_Text.text = autoplaySpinsRemaining < 0 ? "∞" : autoplaySpinsRemaining.ToString();
    }

    private void UpdateFreeSpinDisplay()
    {
        if (FreeSpinPanel != null)
        {
            FreeSpinPanel.SetActive(waitingForFreeSpinStart);
        }

        if (FreeSpinCountPanel != null)
        {
            FreeSpinCountPanel.SetActive(IsFreeSpin && !waitingForFreeSpinStart);
        }

        if (FreeSpinWinPanel != null)
        {
            FreeSpinWinPanel.SetActive(waitingForFreeSpinTake);
        }

        if (Take_Button != null)
        {
            Take_Button.gameObject.SetActive(waitingForFreeSpinTake);
        }

        UpdateAutoplayDisplay();

        SetFreeSpinNumberImage(FreeSpinCounter_Image, Math.Max(0, freeSpinsUsed));
        SetFreeSpinNumberImage(FreeSpinTotal_Image, Math.Max(0, freeSpinsTotal));
    }

    private void CacheFreeSpinNumberSprites()
    {
        freeSpinNumberSprites.Clear();

        foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite == null || !sprite.name.StartsWith("numbers_", StringComparison.Ordinal) ||
                !int.TryParse(sprite.name.Substring("numbers_".Length), out int value))
            {
                continue;
            }

            freeSpinNumberSprites[value] = sprite;
        }

        CacheCurrentNumberSprite(FreeSpinCounter_Image);
        CacheCurrentNumberSprite(FreeSpinTotal_Image);
    }

    private void CacheCurrentNumberSprite(Image image)
    {
        Sprite sprite = image != null ? image.sprite : null;
        if (sprite != null && sprite.name.StartsWith("numbers_", StringComparison.Ordinal) &&
            int.TryParse(sprite.name.Substring("numbers_".Length), out int value))
        {
            freeSpinNumberSprites[value] = sprite;
        }
    }

    private void SetFreeSpinNumberImage(Image image, int value)
    {
        if (image == null || image.sprite == null)
        {
            return;
        }

        int displayValue = Mathf.Clamp(value, 0, FreeSpinNumberRects.Length - 1);
        if (displayValue != value && !freeSpinNumberRangeWarningShown)
        {
            freeSpinNumberRangeWarningShown = true;
            Debug.LogWarning("[SlotBehaviour] The free-spin number artwork supports values from 0 to 11.");
        }

        if (!freeSpinNumberSprites.TryGetValue(displayValue, out Sprite sprite) || sprite == null)
        {
            Texture2D texture = image.sprite.texture;
            if (texture == null)
            {
                return;
            }

            sprite = Sprite.Create(
                texture,
                FreeSpinNumberRects[displayValue],
                new Vector2(0.5f, 0.5f),
                image.sprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = $"numbers_{displayValue}_runtime";
            freeSpinNumberSprites[displayValue] = sprite;
            generatedFreeSpinNumberSprites.Add(sprite);
        }

        image.sprite = sprite;
    }

    private void UpdateBalanceText(double balance)
    {
        balanceTween?.Kill();
        if (Balance_text == null)
        {
            return;
        }

        bool hasPrefix = Balance_text.text.TrimStart().StartsWith("BALANCE", StringComparison.OrdinalIgnoreCase);
        Balance_text.text = hasPrefix
            ? $"BALANCE:  {FormatAmount(balance)}"
            : FormatAmount(balance);
    }

    private void AnimateBalanceTo(double targetBalance)
    {
        balanceTween?.Kill();
        if (Balance_text == null)
        {
            return;
        }

        double displayed = currentBalance;
        bool hasPrefix = Balance_text.text.TrimStart().StartsWith("BALANCE", StringComparison.OrdinalIgnoreCase);
        balanceTween = DOTween.To(
                () => displayed,
                value =>
                {
                    displayed = value;
                    if (Balance_text != null)
                    {
                        string amount = FormatAmount(displayed);
                        Balance_text.text = hasPrefix ? $"BALANCE:  {amount}" : amount;
                    }
                },
                targetBalance,
                0.45f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void UpdateWinLineCount(int count)
    {
        if (WinLinesCount_Text != null)
        {
            WinLinesCount_Text.text = Math.Max(0, count).ToString();
        }
    }

    private int GetRowCount()
    {
        return gameConfig != null && gameConfig.rowCount > 0 ? gameConfig.rowCount : DefaultRowCount;
    }

    private List<List<int>> GenerateRandomMatrix(int rowCount)
    {
        int symbolCount = myImages != null && myImages.Length > 0
            ? myImages.Length
            : gameConfig != null && gameConfig.symbolCount > 0
                ? gameConfig.symbolCount
                : 1;

        List<List<int>> matrix = new List<List<int>>();
        int reelCount = reels.Count > 0 ? reels.Count : DefaultReelCount;
        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            List<int> column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(UnityEngine.Random.Range(0, symbolCount));
            }

            matrix.Add(column);
        }

        return matrix;
    }

    private static string FormatAmount(double amount)
    {
        return amount.ToString("0.00##");
    }

    private static void PlayAudio(AudioSource audioSource)
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.PlayOneShot(audioSource.clip);
        }
    }

    private static void PlayLoopingAudio(AudioSource audioSource)
    {
        if (audioSource == null || audioSource.clip == null)
        {
            return;
        }

        audioSource.loop = true;
        audioSource.Play();
    }

    private static void StopLoopingAudio(AudioSource audioSource)
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void KillReelTweens(bool restorePositions)
    {
        foreach (ReelRuntime reel in reels)
        {
            reel.motionTween?.Kill();
            reel.motionTween = null;
            if (restorePositions && reel.transform != null)
            {
                reel.transform.anchoredPosition = reel.restingPosition;
            }
        }
    }

    private void KillAllTweens()
    {
        KillReelTweens(true);

        foreach (Tween tween in activeTweens)
        {
            tween?.Kill();
        }

        activeTweens.Clear();
        balanceTween?.Kill();
        balanceTween = null;
        winAmountTween?.Kill();
        winAmountTween = null;
    }

    #endregion
}

[Serializable]
public class SlotImage
{
    public List<Image> slotImages = new List<Image>();
}
