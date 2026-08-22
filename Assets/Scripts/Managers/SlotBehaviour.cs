using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable]
internal sealed class SpinResultEvent : UnityEvent<SpinResult>
{
}

[Serializable]
internal sealed class SymbolSelectedEvent : UnityEvent<int, int, int>
{
}

/// <summary>
/// Owns the complete visual slot flow. The server remains authoritative for the
/// matrix, wins, balance, expanding wilds and gift wilds.
///
/// The controller is wired to the existing SlotManager in MainScene. A runtime
/// fallback remains for compatibility with scenes that have not been migrated.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class SlotBehaviour : MonoBehaviour
{
    private const int DefaultReelCount = 5;
    private const int DefaultRowCount = 3;
    private const float NormalAllWinBoxesDuration = 2f;
    private const float NormalSingleWinLineDuration = 2f;
    private const float ExtraGiftWildRevealDelay = 2f;
    private const int DecimalPointSpriteIndex = 10;
    private const int CommaSpriteIndex = 11;

    [Header("Symbol Sprites - Assign by Name")]
    [SerializeField] private Sprite sprite10;
    [SerializeField] private Sprite spriteA;
    [SerializeField] private Sprite spriteBell;
    [SerializeField] private Sprite spriteCandle;
    [SerializeField] private Sprite spriteCup;
    [SerializeField] private Sprite spriteDeer;
    [SerializeField] private Sprite spriteGift;
    [SerializeField] private Sprite spriteJ;
    [SerializeField] private Sprite spriteK;
    [SerializeField] private Sprite spriteMoon;
    [SerializeField] private Sprite spriteQ;
    [SerializeField] private Sprite spriteSanta;
    [SerializeField] private Sprite spriteSocks;

    [HideInInspector]
    [SerializeField] private Sprite[] myImages = Array.Empty<Sprite>();

    [Header("Slot Images")]
    [SerializeField] private List<SlotImage> images = new List<SlotImage>();
    [SerializeField] private List<SlotImage> Tempimages = new List<SlotImage>();

    [Header("Slot Transforms")]
    [SerializeField] private Transform[] Slot_Transform = Array.Empty<Transform>();

    [Header("Miscellaneous UI")]
    [SerializeField] private TMP_Text TotalWin_text;
    [SerializeField] private TMP_Text winLabelText;
    [SerializeField] private TMP_Text goodLuckText;
    [SerializeField] private TMP_Text WinLinesCount_Text;

    [Header("Portrait Miscellaneous UI")]
    [SerializeField] private TMP_Text portraitTotalWinText;
    [SerializeField] private TMP_Text portraitWinLabelText;
    [SerializeField] private TMP_Text portraitGoodLuckText;
    [SerializeField] private TMP_Text portraitWinLinesCountText;

    [Header("Reel Win Amounts")]
    [SerializeField] private GameObject reelWinAmountRoot;
    [FormerlySerializedAs("reelTotalWinText")]
    [SerializeField] private TMP_Text middleWinText;
    [SerializeField] private TMP_Text topWinText;
    [SerializeField] private TMP_Text bottomWinText;
    [SerializeField, Min(1f)] private float reelWinOvershootScale = 1.18f;
    [SerializeField, Min(0f)] private float reelWinGrowDuration = 0.18f;
    [SerializeField, Min(0f)] private float reelWinSlamDuration = 0.08f;

    [Header("Spin Timing")]
    [SerializeField, Min(0.1f)] private float normalMinimumSpinTime = 1.8f;
    [SerializeField, Min(0.05f)] private float fastMinimumSpinTime = 0.75f;
    [SerializeField, Min(1)] private int minSpinCyclesBeforeStop = 3;
    [SerializeField, Min(0f)] private float reelStartStagger = 0.08f;
    [SerializeField, Min(0f)] private float reelStopStagger = 0.12f;
    [SerializeField, Min(0.1f)] private float resultTimeout = 12f;
    [SerializeField, Min(100f)] private float normalReelSpeed = 4700f;
    [SerializeField, Min(100f)] private float fastReelSpeed = 6000f;
    [SerializeField, Min(0f)] private float stopAnticipationDistance = 20f;
    [SerializeField, Min(0f)] private float stopOvershootDistance = 118f;
    [SerializeField, Min(0.01f)] private float stopOvershootDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float stopSettleDuration = 0.3f;

    [Header("Win Line Presentation")]
    [SerializeField, Min(0.01f)] private float singleWinLineDuration = 0.7f;

    [Header("Win Line Visuals")]
    [Tooltip("Result line ID 0 maps to element 0 (Line_1), ID 1 maps to element 1 (Line_2), and so on.")]
    [SerializeField] private GameObject[] winLineObjects = Array.Empty<GameObject>();

    [Header("Win Boxes")]
    [Tooltip("The shared parent containing the WinBox column groups, used for normal and free-spin wins.")]
    [SerializeField] private Transform freeSpinWinBoxesRoot;
    [Tooltip("The five WinBox column containers in left-to-right reel order. Each column contains three symbol rows.")]
    [SerializeField] private Transform[] winAnimationColumns = new Transform[DefaultReelCount];
    [SerializeField, Min(0.1f)] private float freeSpinWinBoxDuration = 3f;

    [Header("Win Animation Sprites")]
    [SerializeField] private List<Sprite> animSprites10 = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesA = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesBell = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesCandle = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesCup = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesDeer = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesGift = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesJ = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesK = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesMoon = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesQ = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesSanta = new List<Sprite>();
    [SerializeField] private List<Sprite> animSpritesSocks = new List<Sprite>();
    [SerializeField, Min(0.1f)] private float winSymbolLoopDuration = 1.2f;

    [Header("Free Spin Start Transition")]
    [SerializeField, Min(0f)] private float freeSpinSymbolFadeOutDuration = 1f;
    [SerializeField, Min(0f)] private float freeSpinSymbolFadeInDuration = 1f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource spinAudio;
    [SerializeField] private AudioSource winAudio;

    [Header("Behaviour Events (Optional)")]
    [SerializeField] private UnityEvent onSpinStarted = new UnityEvent();
    [SerializeField] private SpinResultEvent onSpinStopped = new SpinResultEvent();
    [SerializeField] private UnityEvent onSpinRequestFailed = new UnityEvent();
    [SerializeField] private SymbolSelectedEvent onSymbolSelected = new SymbolSelectedEvent();
    [SerializeField] private UnityEvent onSymbolInfoDismissed = new UnityEvent();

    internal event Action<SpinResult> RoundStopped;
    internal event Action<SpinResult> RequiredPresentationCompleted;
    internal event Action<string> PresentationFailed;
    internal event Action SpinControlPresentationChanged;

    internal bool IsCurrentlySpinning => IsSpinning;
    internal bool IsInitialized => isInitialized;
    internal bool IsWaitingForLateResult => waitingForLateResult;
    internal bool IsResultPresentationActive => resultPresentationInProgress;
    internal bool IsStopRequested => stopSpinRequested;
    internal bool IsExtraGiftWildRevealActive => extraGiftWildRevealActive;
    internal double FreeSpinServerTotalWin => Math.Max(0d, freeSpinServerTotalWin);
    internal bool CanBeginSpinPresentation => isInitialized && !IsSpinning && freeSpinStartSymbolTween == null &&
        !waitingForLateResult && !resultPresentationInProgress && reels.Count > 0 &&
        myImages != null && myImages.Any(sprite => sprite != null);

    private readonly List<ReelRuntime> reels = new List<ReelRuntime>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly Dictionary<int, Sprite> serverSpritesById = new Dictionary<int, Sprite>();
    private readonly List<int> mappedServerSymbolIds = new List<int>();
    private readonly HashSet<int> reportedUnknownSymbolIds = new HashSet<int>();
    private readonly HashSet<int> reportedMissingWinLineIds = new HashSet<int>();
    private readonly List<List<GameObject>> freeSpinWinBoxes = new List<List<GameObject>>();
    private readonly List<List<WinningSymbolAnimationRuntime>> winningSymbolAnimations =
        new List<List<WinningSymbolAnimationRuntime>>();
    private bool serverSymbolMappingActive;

    internal List<List<int>> currentDisplayMatrix;

    private GameConfig gameConfig;
    private GameManager gameManager;
    private SpinResult pendingResult;
    private SpinResult lastPresentedResult;

    private Coroutine spinRoutine;
    private Coroutine winAnimationRoutine;
    private Tween winAmountTween;
    private Tween reelWinAmountTween;
    private Tween freeSpinStartSymbolTween;

    private bool gameManagerEventsBound;
    private bool isInitialized;
    private bool IsSpinning;
    private bool stopSpinRequested;
    private bool resultReceived;
    private bool resultFailed;
    private bool waitingForLateResult;
    private bool resultPresentationInProgress;
    private bool extraGiftWildRevealActive;
    private bool autoplayRoundInProgress;
    private bool requiredPresentationCompletionRaised;
    private bool shuttingDown;
    private bool freeSpinWinPresentationActive;
    private double freeSpinServerTotalWin;
    private int freeSpinWinDecimalPlaces;
    private int currentSpinReelWinDecimalPlaces = -1;
    private readonly Dictionary<TMP_Text, Vector3> reelWinOriginalScales =
        new Dictionary<TMP_Text, Vector3>();

    private SpinSpeed spinSpeed = SpinSpeed.Normal;

    private sealed class ReelRuntime
    {
        internal RectTransform transform;
        internal readonly List<Image> symbols = new List<Image>();
        internal Vector2 restingPosition;
        internal float symbolPitch;
        internal Tween motionTween;
        internal float motionBasePixelsPerSecond;
        internal int completedCycles;
    }

    private sealed class WinningSymbolAnimationRuntime
    {
        internal GameObject root;
        internal Image renderer;
        internal ImageAnimation animation;
        internal Image staticSymbol;
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
        ShowGoodLuckState();
        BuildSymbolSpriteArrays();
        BindGameManagerEvents();
        BuildReelCache();
        BuildFreeSpinWinBoxCache();
        HideAllWinLineVisuals();
        HideFreeSpinWinBoxes();
        HideReelWinAmounts();
    }

    private void Start()
    {
        if (!isInitialized && gameManager != null && gameManager.IsInitialized)
        {
            ApplyInitialization(
                gameManager.GameConfig,
                gameManager.PlayerData,
                GenerateRandomMatrix(gameManager.GameConfig != null ? gameManager.GameConfig.rowCount : DefaultRowCount));
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
        SetExtraGiftWildRevealActive(false);
        UnbindGameManagerEvents();
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
    }

    #region Scene setup

    private void ResolveSceneReferences()
    {
        TotalWin_text = TotalWin_text != null ? TotalWin_text : FindNamedText("WinAmount", "TotalWin");
        winLabelText = winLabelText != null ? winLabelText : FindNamedText("WinText");
        goodLuckText = goodLuckText != null ? goodLuckText : FindNamedText("GoodLuckText");
        WinLinesCount_Text = WinLinesCount_Text != null ? WinLinesCount_Text : FindNamedText("WinLinesCount");

        Transform portraitUiRoot = FindSceneTransform("PortraitUI");
        portraitTotalWinText = portraitTotalWinText != null
            ? portraitTotalWinText
            : FindNamedComponentInChildren<TMP_Text>(portraitUiRoot, "WinAmount", "TotalWin");
        portraitWinLabelText = portraitWinLabelText != null
            ? portraitWinLabelText
            : FindNamedComponentInChildren<TMP_Text>(portraitUiRoot, "WinText");
        portraitGoodLuckText = portraitGoodLuckText != null
            ? portraitGoodLuckText
            : FindNamedComponentInChildren<TMP_Text>(portraitUiRoot, "GoodLuckText");
        portraitWinLinesCountText = portraitWinLinesCountText != null
            ? portraitWinLinesCountText
            : FindNamedComponentInChildren<TMP_Text>(portraitUiRoot, "WinLinesCount");
        ResolveReelWinAmountReferences();

        ApplyToTexts(TotalWin_text, portraitTotalWinText, text =>
        {
            text.enableAutoSizing = false;
            text.transform.localScale = Vector3.one;
        });

        SetDeferredFreeSpinUiActive(false);
    }

    private void ResolveReelWinAmountReferences()
    {
        if (reelWinAmountRoot == null)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid() || candidate.name != "TotalWin")
                {
                    continue;
                }

                TMP_Text[] childTexts = candidate.GetComponentsInChildren<TMP_Text>(true);
                if (childTexts.Any(text => text.name == "TopWin") &&
                    childTexts.Any(text => text.name == "BottomWin"))
                {
                    reelWinAmountRoot = candidate.gameObject;
                    break;
                }
            }
        }

        if (reelWinAmountRoot == null)
        {
            return;
        }

        TMP_Text[] reelWinTexts = reelWinAmountRoot.GetComponentsInChildren<TMP_Text>(true);
        middleWinText = ResolveReelWinSpriteText(middleWinText, reelWinTexts, "MiddleWin");
        topWinText = ResolveReelWinSpriteText(topWinText, reelWinTexts, "TopWin");
        bottomWinText = ResolveReelWinSpriteText(bottomWinText, reelWinTexts, "BottomWin");

        foreach (TMP_Text text in reelWinTexts)
        {
            if (text == null) continue;
            bool isReelWinName = text.name == "TotalWin" || text.name == "MiddleWin" ||
                text.name == "TopWin" || text.name == "BottomWin";
            bool isSelected = text == middleWinText || text == topWinText || text == bottomWinText;
            if (isReelWinName && !isSelected)
            {
                text.gameObject.SetActive(false);
            }
        }

        CacheReelWinTextScale(middleWinText);
        CacheReelWinTextScale(topWinText);
        CacheReelWinTextScale(bottomWinText);
    }

    private static TMP_Text ResolveReelWinSpriteText(
        TMP_Text assigned,
        IEnumerable<TMP_Text> candidates,
        string objectName)
    {
        if (assigned != null && assigned.name == objectName && assigned.spriteAsset != null)
        {
            return assigned;
        }

        TMP_Text spriteText = candidates.FirstOrDefault(text =>
            text != null && text.name == objectName && text.spriteAsset != null);
        if (spriteText != null)
        {
            return spriteText;
        }

        if (assigned != null && assigned.name == objectName)
        {
            return assigned;
        }

        return candidates.FirstOrDefault(text => text != null && text.name == objectName);
    }

    private void CacheReelWinTextScale(TMP_Text target)
    {
        if (target != null && !reelWinOriginalScales.ContainsKey(target))
        {
            reelWinOriginalScales[target] = target.transform.localScale;
        }
    }

    private void BuildSymbolSpriteArrays()
    {
        Sprite[] legacySprites = myImages ?? Array.Empty<Sprite>();
        sprite10 = SpriteOrLegacy(sprite10, legacySprites, 0);
        spriteA = SpriteOrLegacy(spriteA, legacySprites, 1);
        spriteBell = SpriteOrLegacy(spriteBell, legacySprites, 2);
        spriteCandle = SpriteOrLegacy(spriteCandle, legacySprites, 3);
        spriteCup = SpriteOrLegacy(spriteCup, legacySprites, 4);
        spriteDeer = SpriteOrLegacy(spriteDeer, legacySprites, 5);
        spriteGift = SpriteOrLegacy(spriteGift, legacySprites, 6);
        spriteJ = SpriteOrLegacy(spriteJ, legacySprites, 7);
        spriteK = SpriteOrLegacy(spriteK, legacySprites, 8);
        spriteMoon = SpriteOrLegacy(spriteMoon, legacySprites, 9);
        spriteQ = SpriteOrLegacy(spriteQ, legacySprites, 10);
        spriteSanta = SpriteOrLegacy(spriteSanta, legacySprites, 11);
        spriteSocks = SpriteOrLegacy(spriteSocks, legacySprites, 12);

        myImages = new[]
        {
            sprite10,
            spriteA,
            spriteBell,
            spriteCandle,
            spriteCup,
            spriteDeer,
            spriteGift,
            spriteJ,
            spriteK,
            spriteMoon,
            spriteQ,
            spriteSanta,
            spriteSocks
        };
    }

    private static Sprite SpriteOrLegacy(Sprite namedSprite, Sprite[] legacySprites, int symbolId)
    {
        return namedSprite != null
            ? namedSprite
            : symbolId >= 0 && symbolId < legacySprites.Length
                ? legacySprites[symbolId]
                : null;
    }

    private bool BuildServerSymbolMapping(List<SymbolInfo> symbols)
    {
        serverSpritesById.Clear();
        mappedServerSymbolIds.Clear();
        reportedUnknownSymbolIds.Clear();
        serverSymbolMappingActive = false;

        if (symbols == null || symbols.Count == 0)
        {
            Debug.LogWarning("[SlotBehaviour] Init did not provide any symbols; using the legacy sprite order.");
            return false;
        }

        Dictionary<string, int> symbolIdsByName = new Dictionary<string, int>();
        HashSet<int> encounteredSymbolIds = new HashSet<int>();
        bool hasValidationErrors = false;
        foreach (SymbolInfo symbol in symbols)
        {
            if (symbol == null)
            {
                hasValidationErrors = true;
                Debug.LogError("[SlotBehaviour] Init contains a null symbol entry.");
                continue;
            }

            if (!encounteredSymbolIds.Add(symbol.id))
            {
                hasValidationErrors = true;
                Debug.LogError($"[SlotBehaviour] Init contains duplicate symbol id {symbol.id} ({symbol.name}).");
                continue;
            }

            string normalizedName = NormalizeSymbolName(symbol.name);
            if (symbolIdsByName.TryGetValue(normalizedName, out int existingId))
            {
                hasValidationErrors = true;
                Debug.LogError($"[SlotBehaviour] Init contains duplicate symbol name '{symbol.name}' for ids {existingId} and {symbol.id}.");
            }
            else if (!string.IsNullOrEmpty(normalizedName))
            {
                symbolIdsByName.Add(normalizedName, symbol.id);
            }

            if (!TryResolveNamedSymbol(normalizedName, out Sprite sprite))
            {
                hasValidationErrors = true;
                Debug.LogError($"[SlotBehaviour] No assigned sprite matches init symbol id {symbol.id} named '{symbol.name}'.");
                continue;
            }

            if (sprite == null)
            {
                hasValidationErrors = true;
                Debug.LogError($"[SlotBehaviour] The sprite for init symbol id {symbol.id} named '{symbol.name}' is not assigned.");
                continue;
            }

            serverSpritesById.Add(symbol.id, sprite);
            mappedServerSymbolIds.Add(symbol.id);
        }

        serverSymbolMappingActive = mappedServerSymbolIds.Count > 0;
        if (!serverSymbolMappingActive)
        {
            Debug.LogError("[SlotBehaviour] None of the init symbols could be mapped to assigned sprites.");
            return false;
        }

        if (hasValidationErrors || mappedServerSymbolIds.Count != symbols.Count)
        {
            Debug.LogError($"[SlotBehaviour] Mapped {mappedServerSymbolIds.Count} of {symbols.Count} init symbols. Check the symbol names and sprite assignments above.");
            return false;
        }

        Debug.Log($"[SlotBehaviour] Built init symbol mapping for {mappedServerSymbolIds.Count} symbols.");
        return true;
    }

    private bool TryResolveNamedSymbol(
        string normalizedName,
        out Sprite sprite)
    {
        sprite = null;
        bool recognized = true;

        switch (normalizedName)
        {
            case "santa":
                sprite = spriteSanta;
                break;
            case "gift":
            case "present":
                sprite = spriteGift;
                break;
            case "scatter":
            case "moon":
                sprite = spriteMoon;
                break;
            case "reindeer":
            case "deer":
                sprite = spriteDeer;
                break;
            case "bell":
            case "bells":
                sprite = spriteBell;
                break;
            case "stocking":
            case "stockings":
            case "sock":
            case "socks":
                sprite = spriteSocks;
                break;
            case "candle":
                sprite = spriteCandle;
                break;
            case "milkcookie":
            case "milkcookies":
            case "cookie":
            case "cookies":
            case "cup":
                sprite = spriteCup;
                break;
            case "ace":
            case "a":
                sprite = spriteA;
                break;
            case "king":
            case "k":
                sprite = spriteK;
                break;
            case "queen":
            case "q":
                sprite = spriteQ;
                break;
            case "jack":
            case "j":
                sprite = spriteJ;
                break;
            case "ten":
            case "10":
                sprite = sprite10;
                break;
            default:
                recognized = false;
                break;
        }

        return recognized;
    }

    private static string NormalizeSymbolName(string symbolName)
    {
        return string.IsNullOrWhiteSpace(symbolName)
            ? string.Empty
            : new string(symbolName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private void BuildReelCache()
    {
        reels.Clear();
        Transform reelsRoot = FindSceneTransform("Slots");

        if (reelsRoot == null)
        {
            Debug.LogError("[SlotBehaviour] The Slots reel root was not found.");
            return;
        }

        for (int reelIndex = 0; reelIndex < reelsRoot.childCount; reelIndex++)
        {
            RectTransform reelTransform = reelsRoot.GetChild(reelIndex) as RectTransform;
            if (reelTransform == null || !reelTransform.gameObject.activeInHierarchy)
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

        if (myImages == null || myImages.Length == 0 || myImages.All(sprite => sprite == null))
        {
            myImages = reels[0].symbols.Select(symbol => symbol.sprite).ToArray();
            BuildSymbolSpriteArrays();
        }

        images = reels
            .Select(reel => new SlotImage { slotImages = new List<Image>(reel.symbols) })
            .ToList();

        Slot_Transform = reels.Select(reel => (Transform)reel.transform).ToArray();

        RefreshVisibleImageCache(DefaultRowCount);
        SetupSymbolButtons(DefaultRowCount);
    }

    private void BuildFreeSpinWinBoxCache()
    {
        freeSpinWinBoxes.Clear();
        winningSymbolAnimations.Clear();
        if (freeSpinWinBoxesRoot == null)
        {
            freeSpinWinBoxesRoot = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    candidate.name == "Animations" &&
                    candidate.GetComponentsInChildren<Transform>(true).Count(child => child.name == "WinBox") >=
                    DefaultReelCount * DefaultRowCount);
        }

        if (freeSpinWinBoxesRoot == null)
        {
            Debug.LogWarning("[SlotBehaviour] Free-spin WinBox root was not found.", this);
            return;
        }

        List<Transform> columns = Enumerable.Range(0, DefaultReelCount)
            .Select(index => winAnimationColumns != null && index < winAnimationColumns.Length
                ? winAnimationColumns[index]
                : null)
            .ToList();
        if (columns.Any(column => column == null))
        {
            Debug.LogWarning(
                "[SlotBehaviour] Assign all five Win Animation Columns in left-to-right reel order.",
                this);
        }

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            Transform column = columns[columnIndex];
            List<GameObject> columnBoxes = new List<GameObject>();
            List<WinningSymbolAnimationRuntime> columnAnimations =
                new List<WinningSymbolAnimationRuntime>();
            if (column == null)
            {
                freeSpinWinBoxes.Add(Enumerable.Repeat<GameObject>(null, DefaultRowCount).ToList());
                winningSymbolAnimations.Add(
                    Enumerable.Repeat<WinningSymbolAnimationRuntime>(null, DefaultRowCount).ToList());
                continue;
            }

            List<Transform> rows = Enumerable.Range(0, column.childCount)
                .Select(column.GetChild)
                .OrderByDescending(row => row is RectTransform rect ? rect.anchoredPosition.y : row.localPosition.y)
                .Take(DefaultRowCount)
                .ToList();
            int rowCount = rows.Count;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                Transform row = rows[rowIndex];
                Transform symbolAnimation = row.Find("Animations");
                WinningSymbolAnimationRuntime animationRuntime = null;
                if (symbolAnimation != null)
                {
                    animationRuntime = new WinningSymbolAnimationRuntime
                    {
                        root = symbolAnimation.gameObject,
                        renderer = symbolAnimation.GetComponent<Image>() ??
                            symbolAnimation.GetComponentInChildren<Image>(true),
                        animation = symbolAnimation.GetComponent<ImageAnimation>() ??
                            symbolAnimation.GetComponentInChildren<ImageAnimation>(true),
                        staticSymbol = columnIndex < Tempimages.Count &&
                            Tempimages[columnIndex]?.slotImages != null &&
                            rowIndex < Tempimages[columnIndex].slotImages.Count
                                ? Tempimages[columnIndex].slotImages[rowIndex]
                                : null
                    };
                    symbolAnimation.gameObject.SetActive(false);
                }

                Transform winBox = row.Find("WinBox");
                columnBoxes.Add(winBox != null ? winBox.gameObject : null);
                columnAnimations.Add(animationRuntime);
            }

            while (columnBoxes.Count < DefaultRowCount) columnBoxes.Add(null);
            while (columnAnimations.Count < DefaultRowCount) columnAnimations.Add(null);
            freeSpinWinBoxes.Add(columnBoxes);
            winningSymbolAnimations.Add(columnAnimations);
        }

        while (freeSpinWinBoxes.Count < DefaultReelCount)
        {
            freeSpinWinBoxes.Add(Enumerable.Repeat<GameObject>(null, DefaultRowCount).ToList());
            winningSymbolAnimations.Add(
                Enumerable.Repeat<WinningSymbolAnimationRuntime>(null, DefaultRowCount).ToList());
        }
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

    private void SetupSymbolButtons(int rowCount)
    {
        int safeRows = Mathf.Max(1, rowCount);
        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ReelRuntime reel = reels[reelIndex];
            int visibleStart = Mathf.Max(0, reel.symbols.Count - safeRows);
            int visibleCount = Mathf.Min(safeRows, reel.symbols.Count - visibleStart);

            for (int row = 0; row < visibleCount; row++)
            {
                Image symbolImage = reel.symbols[visibleStart + row];
                if (symbolImage == null)
                {
                    continue;
                }

                SymbolButtonHandler handler = symbolImage.GetComponent<SymbolButtonHandler>();
                if (handler == null)
                {
                    handler = symbolImage.gameObject.AddComponent<SymbolButtonHandler>();
                }

                handler.Init(reelIndex, row, this);
            }
        }
    }

    internal void HideSymbolInfoCard()
    {
        onSymbolInfoDismissed?.Invoke();
    }

    internal void OnBetChanged()
    {
        HideSymbolInfoCard();
    }

    internal void OnSymbolClicked(int column, int row, RectTransform symbolRect)
    {
        if (IsSpinning || currentDisplayMatrix == null || column < 0 || column >= currentDisplayMatrix.Count)
        {
            HideSymbolInfoCard();
            return;
        }

        List<int> matrixColumn = currentDisplayMatrix[column];
        if (matrixColumn == null || row < 0 || row >= matrixColumn.Count)
        {
            return;
        }

        int symbolId = matrixColumn[row];
        if (symbolId >= 0)
        {
            onSymbolSelected?.Invoke(symbolId, column, row);
        }
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

    private static void SetDeferredFreeSpinUiActive(bool isActive)
    {
        string[] panelNames = { "FreeSpinPanel", "FreeSpinCountPanel", "FreeSpinWinPanel" };
        foreach (string panelName in panelNames)
        {
            GameObject panel = FindSceneObject(panelName);
            if (panel != null)
            {
                panel.SetActive(isActive);
            }
        }
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

    private static T FindNamedComponentInChildren<T>(
        Transform root,
        params string[] objectNames) where T : Component
    {
        if (root == null) return null;
        return root
            .GetComponentsInChildren<T>(true)
            .FirstOrDefault(candidate => objectNames.Contains(candidate.name));
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
        if (IsSpinning)
        {
            resultFailed = true;
        }
        else if (waitingForLateResult)
        {
            waitingForLateResult = false;
        }
    }

    private void ApplyInitialization(
        GameConfig config,
        PlayerData initialPlayerData,
        List<List<int>> initialMatrix)
    {
        if (config == null)
        {
            Debug.LogError("[SlotBehaviour] Initialization data is incomplete.");
            return;
        }

        try
        {
            gameConfig = config;
            if (!BuildServerSymbolMapping(gameConfig.symbols))
            {
                throw new InvalidOperationException("The init symbol list could not be mapped to the assigned symbol sprites.");
            }
            isInitialized = true;
            waitingForLateResult = false;

            RefreshVisibleImageCache(gameConfig.rowCount);
            SetupSymbolButtons(gameConfig.rowCount);
            bool canUseInitialMatrix = IsValidMatrix(initialMatrix) && MatrixUsesMappedSymbols(initialMatrix);
            ApplyMatrix(canUseInitialMatrix ? initialMatrix : GenerateRandomMatrix(gameConfig.rowCount));
            ShowGoodLuckState();
            UpdateWinLineCount(gameConfig.paylineCount);
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

        PrepareExtraGiftWildSymbols(result);

        if (waitingForLateResult && !IsSpinning)
        {
            StartCoroutine(PresentLateResult(result));
            return;
        }

        pendingResult = result;
        resultReceived = true;
    }

    private void PrepareExtraGiftWildSymbols(SpinResult result)
    {
        if (result?.resultMatrix == null || gameConfig == null)
        {
            return;
        }

        if (result.extraGiftWilds == null)
        {
            return;
        }

        foreach (ServerExtraGiftWild giftWild in result.extraGiftWilds)
        {
            if (!IsValidExtraGiftWild(result, giftWild))
            {
                continue;
            }

            ServerPosition position = giftWild.position;
            result.resultMatrix[position.col][position.row] = giftWild.originalSymbolId;
        }
    }

    private bool IsValidExtraGiftWild(SpinResult result, ServerExtraGiftWild giftWild)
    {
        ServerPosition position = giftWild?.position;
        return result?.resultMatrix != null && gameConfig != null && position != null &&
            position.col >= 0 && position.col < result.resultMatrix.Count &&
            result.resultMatrix[position.col] != null && position.row >= 0 &&
            position.row < result.resultMatrix[position.col].Count;
    }

    private IEnumerator RevealExtraGiftWilds(SpinResult result)
    {
        if (result?.extraGiftWilds == null ||
            !result.extraGiftWilds.Any(giftWild => IsValidExtraGiftWild(result, giftWild)))
        {
            yield break;
        }

        SetExtraGiftWildRevealActive(true);
        yield return new WaitForSecondsRealtime(ExtraGiftWildRevealDelay);

        foreach (ServerExtraGiftWild giftWild in result.extraGiftWilds)
        {
            if (!IsValidExtraGiftWild(result, giftWild))
            {
                continue;
            }

            ServerPosition position = giftWild.position;
            result.resultMatrix[position.col][position.row] = gameConfig.giftWildSymbolId;
        }

        ApplyMatrix(result.resultMatrix);
    }

    private void SetExtraGiftWildRevealActive(bool isActive)
    {
        if (extraGiftWildRevealActive == isActive)
        {
            return;
        }

        extraGiftWildRevealActive = isActive;
        SpinControlPresentationChanged?.Invoke();
    }

    private IEnumerator PresentLateResult(SpinResult result)
    {
        ApplyMatrix(result.resultMatrix);
        yield return RevealExtraGiftWilds(result);

        waitingForLateResult = false;
        PresentResult(result);
        RoundStopped?.Invoke(result);
        SetExtraGiftWildRevealActive(false);
    }

    #endregion

    #region Spin flow

    internal void StartSlots()
    {
        gameManager?.TryStartManualSpin();
    }

    internal void StartSpin()
    {
        StartSlots();
    }

    internal bool QuickStop()
    {
        return gameManager != null && gameManager.RequestStopSpin();
    }

    internal bool BeginSpinPresentation(SpinSpeed speed, bool autoplayRound)
    {
        if (!CanBeginSpinPresentation)
        {
            return false;
        }

        StopWinningAnimations();
        HideSymbolInfoCard();
        if (gameManager != null && gameManager.IsFreeSpinActive)
        {
            ShowFreeSpinWinDisplay(freeSpinServerTotalWin, freeSpinWinDecimalPlaces, false);
        }
        else
        {
            freeSpinWinPresentationActive = false;
            ShowGoodLuckState();
        }

        spinSpeed = speed;
        IsSpinning = true;
        autoplayRoundInProgress = autoplayRound;
        stopSpinRequested = spinSpeed == SpinSpeed.QuickSpin;
        resultReceived = false;
        resultFailed = false;
        pendingResult = null;

        PlayLoopingAudio(spinAudio);
        onSpinStarted?.Invoke();

        spinRoutine = StartCoroutine(SpinCoroutine());
        return true;
    }

    internal void ApplySpinSpeed(SpinSpeed speed)
    {
        if (!IsSpinning) return;

        spinSpeed = speed;
        float targetPixelsPerSecond = spinSpeed == SpinSpeed.Normal
            ? normalReelSpeed
            : fastReelSpeed;

        foreach (ReelRuntime reel in reels)
        {
            if (reel.motionTween == null || !reel.motionTween.IsActive() ||
                reel.motionBasePixelsPerSecond <= 0f)
            {
                continue;
            }

            reel.motionTween.timeScale = targetPixelsPerSecond / reel.motionBasePixelsPerSecond;
        }

        if (spinSpeed == SpinSpeed.QuickSpin)
        {
            stopSpinRequested = true;
        }
    }

    internal void BeginFreeSpinWinPresentation(double initialServerTotalWin, int decimalPlaces)
    {
        freeSpinWinPresentationActive = true;
        freeSpinServerTotalWin = Math.Max(0d, initialServerTotalWin);
        freeSpinWinDecimalPlaces = decimalPlaces >= 0
            ? Mathf.Clamp(decimalPlaces, 0, 28)
            : GetDecimalPlaces(freeSpinServerTotalWin);
        ShowFreeSpinWinDisplay(freeSpinServerTotalWin, freeSpinWinDecimalPlaces, false);
    }

    internal bool PlayFreeSpinStartSymbolTransition(Action onComplete)
    {
        if (!isInitialized || IsSpinning || freeSpinStartSymbolTween != null)
        {
            return false;
        }

        List<Image> visibleSymbols = GetVisibleSymbolImages();
        if (visibleSymbols.Count == 0)
        {
            ApplyMatrix(GenerateRandomMatrix(GetRowCount()));
            onComplete?.Invoke();
            return true;
        }

        SetSymbolAlpha(visibleSymbols, 1f);

        Sequence transition = DOTween.Sequence().SetUpdate(true);
        foreach (Image symbol in visibleSymbols)
        {
            transition.Join(
                symbol
                    .DOFade(0f, freeSpinSymbolFadeOutDuration)
                    .SetEase(Ease.InOutSine));
        }

        transition.AppendCallback(() => ApplyMatrix(GenerateRandomMatrix(GetRowCount())));
        foreach (Image symbol in visibleSymbols)
        {
            transition.Join(
                symbol
                    .DOFade(1f, freeSpinSymbolFadeInDuration)
                    .SetEase(Ease.InOutSine));
        }

        transition.OnComplete(() =>
        {
            SetSymbolAlpha(visibleSymbols, 1f);
            activeTweens.Remove(transition);
            freeSpinStartSymbolTween = null;
            onComplete?.Invoke();
        });

        freeSpinStartSymbolTween = transition;
        activeTweens.Add(transition);
        return true;
    }

    internal void CancelFreeSpinStartSymbolTransition()
    {
        Tween transition = freeSpinStartSymbolTween;
        freeSpinStartSymbolTween = null;
        transition?.Kill();
        if (transition != null) activeTweens.Remove(transition);
        SetSymbolAlpha(GetVisibleSymbolImages(), 1f);
    }

    private List<Image> GetVisibleSymbolImages()
    {
        return Tempimages
            .Where(group => group?.slotImages != null)
            .SelectMany(group => group.slotImages)
            .Where(symbol => symbol != null)
            .Distinct()
            .ToList();
    }

    private static void SetSymbolAlpha(IEnumerable<Image> symbols, float alpha)
    {
        float safeAlpha = Mathf.Clamp01(alpha);
        foreach (Image symbol in symbols)
        {
            if (symbol == null) continue;
            Color color = symbol.color;
            color.a = safeAlpha;
            symbol.color = color;
        }
    }

    internal void EndFreeSpinWinPresentation(bool resetDisplay)
    {
        freeSpinWinPresentationActive = false;
        if (resetDisplay) ShowGoodLuckState();
    }

    internal string ShowFreeSpinCompletionWin(double totalFreeGamesWin)
    {
        double safeAmount = Math.Max(0d, totalFreeGamesWin);
        int safeDecimalPlaces = Mathf.Clamp(freeSpinWinDecimalPlaces, 0, 28);

        // Finish the final round's count-up before replacing it with the
        // complete feature total shown on the completion screen.
        if (winAmountTween != null && winAmountTween.IsActive())
        {
            winAmountTween.Complete();
        }
        winAmountTween = null;

        ShowFreeSpinWinDisplay(safeAmount, safeDecimalPlaces, false);
        return FormatAmount(safeAmount, safeDecimalPlaces);
    }

    internal bool RequestStopPresentation()
    {
        if (!IsSpinning || stopSpinRequested)
        {
            return false;
        }

        stopSpinRequested = true;
        return true;
    }

    internal void FailSpinPresentation(string reason)
    {
        if (!IsSpinning && !waitingForLateResult)
        {
            PresentationFailed?.Invoke(reason);
            return;
        }

        resultFailed = true;
    }

    private IEnumerator SpinCoroutine()
    {
        float startTime = Time.realtimeSinceStartup;

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            StartReelMotion(reels[reelIndex]);
            if (spinSpeed == SpinSpeed.Normal && reelIndex < reels.Count - 1)
            {
                float staggerEndsAt = Time.realtimeSinceStartup + reelStartStagger;
                while (spinSpeed == SpinSpeed.Normal && Time.realtimeSinceStartup < staggerEndsAt)
                {
                    yield return null;
                }
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
            while (!stopSpinRequested &&
                   Time.realtimeSinceStartup - startTime < GetMinimumSpinTime())
            {
                yield return null;
            }

            while (!stopSpinRequested && spinSpeed == SpinSpeed.Normal &&
                   !reels.All(reel => reel.completedCycles >= minSpinCyclesBeforeStop))
            {
                yield return null;
            }
        }

        StopLoopingAudio(spinAudio);
        yield return StopReelsAndApplyMatrix(pendingResult.resultMatrix);
        yield return RevealExtraGiftWilds(pendingResult);

        SpinResult completedResult = pendingResult;
        pendingResult = null;
        resultReceived = false;
        spinRoutine = null;

        PresentResult(completedResult);
        IsSpinning = false;
        RoundStopped?.Invoke(completedResult);
        SetExtraGiftWildRevealActive(false);
    }

    private IEnumerator AbortSpin(string reason)
    {
        Debug.LogError($"[SlotBehaviour] {reason}");
        StopLoopingAudio(spinAudio);
        KillReelTweens(true);

        IsSpinning = false;
        resultReceived = false;
        resultFailed = false;
        pendingResult = null;
        spinRoutine = null;
        autoplayRoundInProgress = false;
        onSpinRequestFailed?.Invoke();
        PresentationFailed?.Invoke(reason);
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
        reel.motionBasePixelsPerSecond = pixelsPerSecond;

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

        currentDisplayMatrix = CloneMatrix(resultMatrix);

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

        // Stage the reel outside the visible area before swapping in its result
        // sprites, so the new values enter through the landing animation instead
        // of visibly changing at the reel's current position.
        float landingDistance = Mathf.Max(stopAnticipationDistance, reel.symbolPitch * (quickStop ? 0.75f : 2f));
        reel.transform.anchoredPosition = reel.restingPosition + Vector2.up * landingDistance;
        ApplyMatrixColumn(reelIndex, resultColumn);

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

        currentDisplayMatrix = CloneMatrix(matrix);

        for (int reelIndex = 0; reelIndex < reels.Count; reelIndex++)
        {
            ApplyMatrixColumn(reelIndex, matrix[reelIndex]);
            reels[reelIndex].transform.anchoredPosition = reels[reelIndex].restingPosition;
        }
    }

    internal void SetInitialMatrix(List<List<int>> matrix)
    {
        if (!IsValidMatrix(matrix))
        {
            Debug.LogWarning("[SlotBehaviour] Ignored an invalid initial matrix.");
            return;
        }

        ApplyMatrix(matrix);
    }

    internal List<List<int>> GetCurrentDisplayMatrix()
    {
        return CloneMatrix(currentDisplayMatrix);
    }

    private static List<List<int>> CloneMatrix(List<List<int>> matrix)
    {
        if (matrix == null)
        {
            return null;
        }

        return matrix
            .Select(column => column != null ? new List<int>(column) : new List<int>())
            .ToList();
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (serverSymbolMappingActive)
        {
            if (serverSpritesById.TryGetValue(symbolId, out Sprite mappedSprite) && mappedSprite != null)
            {
                return mappedSprite;
            }

            if (reportedUnknownSymbolIds.Add(symbolId))
            {
                Debug.LogError($"[SlotBehaviour] Result contains symbol id {symbolId}, but init did not map that id to a sprite.");
            }

            return serverSpritesById.Values.FirstOrDefault(sprite => sprite != null);
        }

        if (symbolId >= 0 && symbolId < myImages.Length && myImages[symbolId] != null)
        {
            return myImages[symbolId];
        }

        Debug.LogWarning($"[SlotBehaviour] Invalid or unassigned symbol id {symbolId}.");
        return myImages.FirstOrDefault(sprite => sprite != null);
    }

    private void ApplyMatrixColumn(int reelIndex, List<int> column)
    {
        ReelRuntime reel = reels[reelIndex];
        int rowCount = Mathf.Min(GetRowCount(), column.Count);
        int visibleStart = Mathf.Max(0, reel.symbols.Count - rowCount);

        for (int row = 0; row < rowCount; row++)
        {
            int symbolId = column[row];
            Sprite symbolSprite = GetSymbolSprite(symbolId);
            if (symbolSprite == null)
            {
                continue;
            }

            reel.symbols[visibleStart + row].sprite = symbolSprite;
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

    private bool MatrixUsesMappedSymbols(List<List<int>> matrix)
    {
        if (!serverSymbolMappingActive)
        {
            return true;
        }

        int reelCount = Mathf.Min(reels.Count, matrix.Count);
        int rowCount = GetRowCount();
        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            for (int row = 0; row < rowCount; row++)
            {
                if (!serverSpritesById.ContainsKey(matrix[reelIndex][row]))
                {
                    return false;
                }
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

        double displayedWin = result.grandTotalWin > 0d ? result.grandTotalWin : result.winAmount;
        bool isFreeSpinResult = freeSpinWinPresentationActive &&
            gameManager != null && gameManager.IsFreeSpinActive;

        if (isFreeSpinResult)
        {
            ShowFreeSpinResultWin(result);
            if (result.winAmount > 0d) PlayAudio(winAudio);
        }
        else if (displayedWin > 0d)
        {
            ShowWinState(displayedWin, result.winAmountDecimalPlaces);
            PlayAudio(winAudio);
        }
        else
        {
            ShowGoodLuckState();
        }

        StartResultAnimations(result);

        onSpinStopped?.Invoke(result);
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

    #region Results, wins and compatibility hooks

    internal void InitializeMatrix()
    {
        int rowCount = GetRowCount();
        ApplyMatrix(GenerateRandomMatrix(rowCount));
    }

    private void StartResultAnimations(SpinResult result)
    {
        StopWinningAnimations();
        if (result == null)
        {
            autoplayRoundInProgress = false;
            CompleteRequiredResultPresentation();
            return;
        }

        lastPresentedResult = result;
        PrepareReelWinLinePresentation(result);
        bool useFreeSpinWinBoxes = result.isFreeSpinResult || (gameManager != null && gameManager.IsFreeSpinActive);
        requiredPresentationCompletionRaised = false;
        resultPresentationInProgress = true;
        winAnimationRoutine = StartCoroutine(PlayResultAnimations(result, useFreeSpinWinBoxes));
    }

    private IEnumerator PlayResultAnimations(SpinResult result, bool useFreeSpinWinBoxes)
    {
        // Let the reel-stop callback reach GameManager before a zero-duration
        // result presentation can report completion in this same frame.
        yield return null;

        if (useFreeSpinWinBoxes)
        {
            yield return PlayFreeSpinWinBoxes(result);

            // A result without WinBox positions can finish immediately. Keep
            // the round alive until its bottom win count-up has been shown.
            Tween amountTween = winAmountTween;
            if (amountTween != null && amountTween.IsActive() && !amountTween.IsComplete())
            {
                yield return amountTween.WaitForCompletion();
            }
        }
        else
        {
            bool triggersFreeGames = result?.freeSpinData != null &&
                result.freeSpinData.isTriggered &&
                result.freeSpinData.spinsAwarded > 0 &&
                !result.isFreeSpinResult;
            bool showIndividualWinLines = !autoplayRoundInProgress && !triggersFreeGames;
            yield return PlayNormalWinPresentation(result, showIndividualWinLines);
        }

        resultPresentationInProgress = false;
        autoplayRoundInProgress = false;
        winAnimationRoutine = null;
        CompleteRequiredResultPresentation();
    }

    private IEnumerator PlayFreeSpinWinBoxes(SpinResult result)
    {
        HideAllWinLineVisuals();
        HideFreeSpinWinBoxes();

        HashSet<int> winningPositions = CollectWinningPositions(result);
        if (winningPositions.Count == 0 || freeSpinWinBoxesRoot == null)
        {
            yield break;
        }

        ShowWinBoxes(winningPositions);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, freeSpinWinBoxDuration));
        HideFreeSpinWinBoxes();
    }

    private IEnumerator PlayNormalWinPresentation(SpinResult result, bool showIndividualWinLines)
    {
        HideAllWinLineVisuals();
        HideFreeSpinWinBoxes();

        HashSet<int> winningPositions = CollectWinningPositions(result);
        if (winningPositions.Count > 0)
        {
            ShowWinBoxes(winningPositions);
            yield return new WaitForSecondsRealtime(NormalAllWinBoxesDuration);
            HideFreeSpinWinBoxes();
        }

        // The required presentation ends after all winning symbols have been
        // shown together once. Individual line cycling remains optional visual
        // feedback and must not keep the Spin button locked.
        CompleteRequiredResultPresentation();

        if (!showIndividualWinLines)
        {
            yield break;
        }

        List<WinLine> validWinLines = result?.winLines?
            .Where(line => line != null && line.positions != null && line.positions.Count > 0)
            .ToList() ?? new List<WinLine>();
        if (validWinLines.Count == 0)
        {
            yield break;
        }

        WaitForSecondsRealtime lineDisplayDelay = new WaitForSecondsRealtime(
            NormalSingleWinLineDuration);
        while (!shuttingDown && !IsSpinning)
        {
            foreach (WinLine winLine in validWinLines)
            {
                if (shuttingDown || IsSpinning)
                {
                    HideAllWinLineVisuals();
                    HideFreeSpinWinBoxes();
                    yield break;
                }

                bool lineVisible = ShowWinLineVisual(winLine.lineId);
                ShowWinBoxes(winLine.positions);
                if (lineVisible) ShowReelLineWinAmounts(winLine);
                yield return lineDisplayDelay;
                HideAllWinLineVisuals();
                HideFreeSpinWinBoxes();
                HideReelWinAmounts();
            }
        }

        HideAllWinLineVisuals();
        HideFreeSpinWinBoxes();
        HideReelWinAmounts();
    }

    private static HashSet<int> CollectWinningPositions(SpinResult result)
    {
        HashSet<int> winningPositions = new HashSet<int>();
        if (result?.winLines != null)
        {
            foreach (WinLine winLine in result.winLines)
            {
                if (winLine?.positions == null) continue;
                foreach (int position in winLine.positions) winningPositions.Add(position);
            }
        }

        if (result?.scatterData?.positions != null && result.scatterData.winAmount > 0d)
        {
            foreach (int position in result.scatterData.positions) winningPositions.Add(position);
        }

        return winningPositions;
    }

    private void ShowWinBoxes(IEnumerable<int> positions)
    {
        HideFreeSpinWinBoxes();
        if (positions == null || freeSpinWinBoxesRoot == null)
        {
            return;
        }

        // Activate the shared parent before enabling a symbol overlay so its
        // ImageAnimation Awake/Invoke lifecycle starts on an active object.
        freeSpinWinBoxesRoot.gameObject.SetActive(true);
        bool showedAnyVisual = false;
        foreach (int position in positions.Distinct())
        {
            int rowIndex = position / DefaultReelCount;
            int columnIndex = position % DefaultReelCount;
            if (columnIndex < 0 || columnIndex >= freeSpinWinBoxes.Count ||
                rowIndex < 0 || rowIndex >= freeSpinWinBoxes[columnIndex].Count)
            {
                continue;
            }

            GameObject winBox = freeSpinWinBoxes[columnIndex][rowIndex];
            if (winBox != null)
            {
                winBox.SetActive(true);
                RestartWinBoxAnimation(winBox);
                showedAnyVisual = true;
            }

            if (StartWinningSymbolAnimation(columnIndex, rowIndex))
            {
                showedAnyVisual = true;
            }
        }

        freeSpinWinBoxesRoot.gameObject.SetActive(showedAnyVisual);
    }

    private static void RestartWinBoxAnimation(GameObject winBox)
    {
        ImageAnimation animation = winBox.GetComponent<ImageAnimation>();
        if (animation == null ||
            animation.rendererDelegate == null ||
            animation.textureArray == null ||
            animation.textureArray.Count == 0)
        {
            return;
        }

        animation.StopAnimation();
        animation.StartAnimation();
    }

    private bool StartWinningSymbolAnimation(int columnIndex, int rowIndex)
    {
        if (currentDisplayMatrix == null ||
            columnIndex < 0 || columnIndex >= winningSymbolAnimations.Count ||
            rowIndex < 0 || rowIndex >= winningSymbolAnimations[columnIndex].Count ||
            columnIndex >= currentDisplayMatrix.Count || currentDisplayMatrix[columnIndex] == null ||
            rowIndex >= currentDisplayMatrix[columnIndex].Count)
        {
            return false;
        }

        WinningSymbolAnimationRuntime runtime = winningSymbolAnimations[columnIndex][rowIndex];
        if (runtime?.root == null || runtime.renderer == null || runtime.animation == null)
        {
            return false;
        }

        if (!TryGetWinAnimation(
                currentDisplayMatrix[columnIndex][rowIndex],
                out List<Sprite> frames))
        {
            return false;
        }

        runtime.animation.StopAnimation();
        runtime.animation.textureArray = new List<Sprite>(frames);
        runtime.animation.rendererDelegate = runtime.renderer;
        runtime.animation.doLoopAnimation = true;
        runtime.animation.delayBetweenLoop = 0f;
        int frameCount = runtime.animation.textureArray.Count;
        runtime.animation.AnimationSpeed = 0.0416666679f * frameCount * frameCount /
            Mathf.Max(0.1f, winSymbolLoopDuration);

        runtime.renderer.color = Color.white;
        runtime.renderer.enabled = true;
        if (runtime.staticSymbol != null)
        {
            runtime.staticSymbol.enabled = false;
        }

        runtime.root.SetActive(true);
        runtime.animation.StartAnimation();
        return true;
    }

    private bool TryGetWinAnimation(
        int symbolId,
        out List<Sprite> frames)
    {
        Sprite symbol = GetSymbolSprite(symbolId);

        if (symbol == sprite10) frames = animSprites10;
        else if (symbol == spriteA) frames = animSpritesA;
        else if (symbol == spriteBell) frames = animSpritesBell;
        else if (symbol == spriteCandle) frames = animSpritesCandle;
        else if (symbol == spriteCup) frames = animSpritesCup;
        else if (symbol == spriteDeer) frames = animSpritesDeer;
        else if (symbol == spriteGift) frames = animSpritesGift;
        else if (symbol == spriteJ) frames = animSpritesJ;
        else if (symbol == spriteK) frames = animSpritesK;
        else if (symbol == spriteMoon) frames = animSpritesMoon;
        else if (symbol == spriteQ) frames = animSpritesQ;
        else if (symbol == spriteSanta) frames = animSpritesSanta;
        else if (symbol == spriteSocks) frames = animSpritesSocks;
        else frames = null;

        return frames != null && frames.Count > 0;
    }

    private void CompleteRequiredResultPresentation()
    {
        resultPresentationInProgress = false;
        autoplayRoundInProgress = false;
        if (requiredPresentationCompletionRaised)
        {
            return;
        }

        requiredPresentationCompletionRaised = true;
        RequiredPresentationCompleted?.Invoke(lastPresentedResult);
    }

    internal void ShowWinLineAnimation(List<WinLine> winLines, Action onComplete)
    {
        StopWinningAnimations();
        winAnimationRoutine = StartCoroutine(PlayWinLineVisuals(winLines, true, onComplete));
    }

    private IEnumerator PlayWinLineVisuals(
        List<WinLine> winLines,
        bool loopIndividualLines,
        Action onRequiredPresentationComplete)
    {
        HideAllWinLineVisuals();
        onRequiredPresentationComplete?.Invoke();

        if (!loopIndividualLines || winLines == null || winLines.Count == 0)
        {
            yield break;
        }

        List<WinLine> validWinLines = winLines.Where(line => line != null).ToList();
        if (validWinLines.Count == 0)
        {
            yield break;
        }

        WaitForSecondsRealtime lineDisplayDelay = new WaitForSecondsRealtime(
            Mathf.Max(0.01f, singleWinLineDuration));

        while (!shuttingDown && !IsSpinning)
        {
            foreach (WinLine winLine in validWinLines)
            {
                if (shuttingDown || IsSpinning)
                {
                    HideAllWinLineVisuals();
                    yield break;
                }

                bool lineVisible = ShowWinLineVisual(winLine.lineId);
                if (lineVisible) ShowReelLineWinAmounts(winLine);
                yield return lineDisplayDelay;
                HideAllWinLineVisuals();
                HideReelWinAmounts();
            }
        }

        HideReelWinAmounts();
    }

    private bool ShowWinLineVisual(int resultLineId)
    {
        HideAllWinLineVisuals();

        if (winLineObjects == null || resultLineId < 0 || resultLineId >= winLineObjects.Length)
        {
            ReportMissingWinLineVisual(resultLineId);
            return false;
        }

        GameObject lineObject = winLineObjects[resultLineId];
        if (lineObject == null)
        {
            ReportMissingWinLineVisual(resultLineId);
            return false;
        }

        lineObject.SetActive(true);
        return true;
    }

    private void PrepareReelWinLinePresentation(SpinResult result)
    {
        double spinWin = Math.Max(0d, result?.winAmount ?? 0d);
        currentSpinReelWinDecimalPlaces = result != null && result.winAmountDecimalPlaces >= 0
            ? Mathf.Clamp(result.winAmountDecimalPlaces, 0, 28)
            : GetDecimalPlaces(spinWin);
        HideReelWinAmounts();
    }

    private void ShowReelLineWinAmounts(WinLine winLine)
    {
        if (reelWinAmountRoot == null || winLine?.positions == null || winLine.positions.Count == 0)
        {
            return;
        }

        int centerReel = DefaultReelCount / 2;
        int representativePosition = winLine.positions
            .Where(position => position >= 0 && position < DefaultReelCount * DefaultRowCount)
            .OrderBy(position => Math.Abs(position % DefaultReelCount - centerReel))
            .DefaultIfEmpty(-1)
            .First();
        if (representativePosition < 0)
        {
            HideReelWinAmounts();
            return;
        }

        int row = representativePosition / DefaultReelCount;
        TMP_Text target = row == 0
            ? topWinText
            : row == 2
                ? bottomWinText
                : middleWinText;
        double lineWin = Math.Max(0d, winLine.winAmount);
        ShowSingleReelWinAmount(target, lineWin, true);
    }

    private void ShowSingleReelWinAmount(TMP_Text target, double amount, bool animate)
    {
        if (reelWinAmountRoot == null || target == null)
        {
            return;
        }

        reelWinAmountTween?.Kill();
        reelWinAmountTween = null;
        HideReelWinTextObjects();

        double safeAmount = Math.Max(0d, amount);
        int lineDecimalPlaces = Math.Max(
            currentSpinReelWinDecimalPlaces,
            GetDecimalPlaces(safeAmount));
        target.text = FormatReelWinAmount(target, safeAmount, lineDecimalPlaces);
        reelWinAmountRoot.SetActive(true);
        target.gameObject.SetActive(true);

        Vector3 originalScale = GetReelWinOriginalScale(target);
        if (!animate)
        {
            target.transform.localScale = originalScale;
            return;
        }

        target.transform.localScale = Vector3.zero;
        Sequence popSequence = DOTween.Sequence().SetUpdate(true);
        popSequence.Append(
            target.transform
                .DOScale(originalScale * Mathf.Max(1f, reelWinOvershootScale), reelWinGrowDuration)
                .SetEase(Ease.OutCubic));
        popSequence.Append(
            target.transform
                .DOScale(originalScale, reelWinSlamDuration)
                .SetEase(Ease.InQuad));
        popSequence.OnComplete(() =>
        {
            target.transform.localScale = originalScale;
            if (reelWinAmountTween == popSequence) reelWinAmountTween = null;
        });
        reelWinAmountTween = popSequence;
    }

    private Vector3 GetReelWinOriginalScale(TMP_Text target)
    {
        if (target == null)
        {
            return Vector3.one;
        }

        CacheReelWinTextScale(target);
        return reelWinOriginalScales.TryGetValue(target, out Vector3 originalScale)
            ? originalScale
            : Vector3.one;
    }

    private void HideReelWinTextObjects()
    {
        TMP_Text[] targets = { middleWinText, topWinText, bottomWinText };
        foreach (TMP_Text target in targets)
        {
            if (target == null) continue;
            target.transform.localScale = GetReelWinOriginalScale(target);
            target.gameObject.SetActive(false);
        }
    }

    private void HideReelWinAmounts()
    {
        reelWinAmountTween?.Kill();
        reelWinAmountTween = null;
        HideReelWinTextObjects();
        if (reelWinAmountRoot != null) reelWinAmountRoot.SetActive(false);
    }

    private void HideAllWinLineVisuals()
    {
        if (winLineObjects == null)
        {
            return;
        }

        for (int i = 0; i < winLineObjects.Length; i++)
        {
            if (winLineObjects[i] != null)
            {
                winLineObjects[i].SetActive(false);
            }
        }
    }

    private void HideFreeSpinWinBoxes()
    {
        foreach (List<GameObject> column in freeSpinWinBoxes)
        {
            foreach (GameObject winBox in column)
            {
                if (winBox != null) winBox.SetActive(false);
            }
        }

        foreach (List<WinningSymbolAnimationRuntime> column in winningSymbolAnimations)
        {
            foreach (WinningSymbolAnimationRuntime runtime in column)
            {
                if (runtime?.animation != null)
                {
                    runtime.animation.StopAnimation();
                }

                if (runtime?.root != null)
                {
                    runtime.root.SetActive(false);
                }

                if (runtime?.staticSymbol != null)
                {
                    Color color = runtime.staticSymbol.color;
                    runtime.staticSymbol.color = new Color(color.r, color.g, color.b, 1f);
                    runtime.staticSymbol.enabled = true;
                }
            }
        }

        if (freeSpinWinBoxesRoot != null) freeSpinWinBoxesRoot.gameObject.SetActive(false);
    }

    private void ReportMissingWinLineVisual(int resultLineId)
    {
        if (reportedMissingWinLineIds.Add(resultLineId))
        {
            Debug.LogWarning(
                $"[SlotBehaviour] Result line ID {resultLineId} has no assigned win-line visual.",
                this);
        }
    }

    private void StopWinningAnimations()
    {
        resultPresentationInProgress = false;
        if (winAnimationRoutine != null)
        {
            StopCoroutine(winAnimationRoutine);
            winAnimationRoutine = null;
        }

        HideAllWinLineVisuals();
        HideFreeSpinWinBoxes();
        HideReelWinAmounts();
    }

    private void SetWinAmount(double amount, bool animate, int decimalPlaces, double startAmount = 0d)
    {
        winAmountTween?.Kill();

        if (TotalWin_text == null && portraitTotalWinText == null)
        {
            return;
        }

        if (!animate || amount <= 0d)
        {
            SetWinAmountText(FormatAmount(Math.Max(0d, amount), decimalPlaces));
            return;
        }

        double displayed = Math.Max(0d, startAmount);
        SetWinAmountText(FormatAmount(displayed, decimalPlaces));
        winAmountTween = DOTween.To(
                () => displayed,
                value =>
                {
                    displayed = value;
                    SetWinAmountText(FormatAmount(displayed, decimalPlaces));
                },
                amount,
                0.65f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void ShowGoodLuckState()
    {
        winAmountTween?.Kill();
        winAmountTween = null;

        ApplyToTexts(TotalWin_text, portraitTotalWinText, text =>
        {
            text.text = FormatAmount(0d, 0);
            text.transform.localScale = Vector3.one;
            text.gameObject.SetActive(false);
        });
        SetTextState(winLabelText, portraitWinLabelText, false);
        SetTextState(goodLuckText, portraitGoodLuckText, true);
    }

    private void ShowWinState(double amount, int decimalPlaces)
    {
        int safeDecimalPlaces = decimalPlaces >= 0
            ? Mathf.Clamp(decimalPlaces, 0, 28)
            : GetDecimalPlaces(amount);

        SetTextState(goodLuckText, portraitGoodLuckText, false);
        SetTextState(winLabelText, portraitWinLabelText, true);
        ApplyToTexts(TotalWin_text, portraitTotalWinText, text =>
        {
            text.text = FormatAmount(0d, safeDecimalPlaces);
            text.transform.localScale = Vector3.one;
            text.gameObject.SetActive(true);
        });

        SetWinAmount(amount, true, safeDecimalPlaces);
    }

    private void ShowFreeSpinResultWin(SpinResult result)
    {
        double previousTotal = Math.Max(0d, freeSpinServerTotalWin);
        double serverTotal = Math.Max(0d, result?.serverTotalRoundWin ?? 0d);
        int resultDecimalPlaces = result != null && result.winAmountDecimalPlaces >= 0
            ? Mathf.Clamp(result.winAmountDecimalPlaces, 0, 28)
            : GetDecimalPlaces(serverTotal);
        freeSpinWinDecimalPlaces = resultDecimalPlaces;

        freeSpinServerTotalWin = serverTotal;
        bool totalChanged = Math.Abs(serverTotal - previousTotal) > 0.0000001d;
        ShowFreeSpinWinDisplay(serverTotal, freeSpinWinDecimalPlaces, totalChanged, previousTotal);
    }

    private void ShowFreeSpinWinDisplay(
        double amount,
        int decimalPlaces,
        bool animate,
        double startAmount = 0d)
    {
        int safeDecimalPlaces = Mathf.Clamp(decimalPlaces, 0, 28);
        SetTextState(goodLuckText, portraitGoodLuckText, false);
        SetTextState(winLabelText, portraitWinLabelText, true);
        SetTextState(TotalWin_text, portraitTotalWinText, true);

        SetWinAmount(amount, animate, safeDecimalPlaces, startAmount);
    }

    #endregion

    #region UI and cleanup helpers

    private void UpdateWinLineCount(int count)
    {
        string value = Math.Max(0, count).ToString();
        ApplyToTexts(WinLinesCount_Text, portraitWinLinesCountText, text => text.text = value);
    }

    private void SetWinAmountText(string value)
    {
        ApplyToTexts(TotalWin_text, portraitTotalWinText, text => text.text = value);
    }

    private static void SetTextState(TMP_Text first, TMP_Text second, bool active)
    {
        ApplyToTexts(first, second, text =>
        {
            text.transform.localScale = Vector3.one;
            text.gameObject.SetActive(active);
        });
    }

    private static void ApplyToTexts(TMP_Text first, TMP_Text second, Action<TMP_Text> action)
    {
        if (action == null) return;
        if (first != null) action(first);
        if (second != null && second != first) action(second);
    }

    private int GetRowCount()
    {
        return gameConfig != null && gameConfig.rowCount > 0 ? gameConfig.rowCount : DefaultRowCount;
    }

    private int GetReelCount()
    {
        return gameConfig != null && gameConfig.reelCount > 0
            ? gameConfig.reelCount
            : reels.Count > 0
                ? reels.Count
                : DefaultReelCount;
    }

    private List<List<int>> GenerateRandomMatrix(int rowCount)
    {
        if (serverSymbolMappingActive && mappedServerSymbolIds.Count > 0)
        {
            return GenerateRandomMatrixFromIds(rowCount, mappedServerSymbolIds);
        }

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

    private List<List<int>> GenerateRandomMatrixFromIds(int rowCount, List<int> symbolIds)
    {
        List<List<int>> matrix = new List<List<int>>();
        int reelCount = reels.Count > 0 ? reels.Count : DefaultReelCount;
        for (int reelIndex = 0; reelIndex < reelCount; reelIndex++)
        {
            List<int> column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(symbolIds[UnityEngine.Random.Range(0, symbolIds.Count)]);
            }

            matrix.Add(column);
        }

        return matrix;
    }

    private static int GetDecimalPlaces(double amount)
    {
        string amountText = amount.ToString("0.################", CultureInfo.InvariantCulture);
        int decimalPoint = amountText.IndexOf('.');
        return decimalPoint < 0 ? 0 : amountText.Length - decimalPoint - 1;
    }

    private static string FormatAmount(double amount, int decimalPlaces)
    {
        int safeDecimalPlaces = Math.Max(0, Math.Min(28, decimalPlaces));
        string format = safeDecimalPlaces == 0
            ? "0"
            : "0." + new string('0', safeDecimalPlaces);
        return amount.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatReelWinAmount(TMP_Text target, double amount, int decimalPlaces)
    {
        int safeDecimalPlaces = Math.Max(0, Math.Min(28, decimalPlaces));
        string format = safeDecimalPlaces == 0
            ? "#,0"
            : "#,0." + new string('0', safeDecimalPlaces);
        string amountText = amount.ToString(format, CultureInfo.InvariantCulture);

        TMP_SpriteAsset spriteAsset = target != null ? target.spriteAsset : null;
        if (spriteAsset == null || spriteAsset.spriteCharacterTable == null)
        {
            return amountText;
        }

        StringBuilder spriteText = new StringBuilder(amountText.Length * 10);
        foreach (char character in amountText)
        {
            int spriteIndex = GetAmountSpriteIndex(character);
            if (spriteIndex >= 0 && spriteIndex < spriteAsset.spriteCharacterTable.Count &&
                spriteAsset.spriteCharacterTable[spriteIndex] != null)
            {
                spriteText.Append("<sprite=");
                spriteText.Append(spriteIndex);
                spriteText.Append('>');
            }
            else
            {
                spriteText.Append(character);
            }
        }

        return spriteText.ToString();
    }

    private static int GetAmountSpriteIndex(char character)
    {
        if (character >= '0' && character <= '9')
        {
            return character - '0';
        }

        if (character == '.') return DecimalPointSpriteIndex;
        if (character == ',') return CommaSpriteIndex;
        return -1;
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
        CancelFreeSpinStartSymbolTransition();

        foreach (Tween tween in activeTweens)
        {
            tween?.Kill();
        }

        activeTweens.Clear();
        winAmountTween?.Kill();
        winAmountTween = null;
    }

    #endregion
}

[Serializable]
internal class SlotImage
{
    [SerializeField] internal List<Image> slotImages = new List<Image>();
}

public sealed class SymbolButtonHandler : MonoBehaviour, IPointerClickHandler
{
    private int column;
    private int row;
    private SlotBehaviour slotBehaviour;

    internal void Init(int symbolColumn, int symbolRow, SlotBehaviour owner)
    {
        column = symbolColumn;
        row = symbolRow;
        slotBehaviour = owner;
    }

    void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
    {
        if (slotBehaviour == null)
        {
            return;
        }

        slotBehaviour.OnSymbolClicked(column, row, transform as RectTransform);
    }
}
