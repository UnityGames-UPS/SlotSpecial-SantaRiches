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
internal sealed class SpinResultEvent : UnityEvent<SpinResult>
{
}

[Serializable]
internal sealed class WinTierEvent : UnityEvent<int, double>
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

    [Header("Symbol Animation Sprites")]
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

    [HideInInspector]
    [SerializeField] private Sprite[] myImages = Array.Empty<Sprite>();

    [Header("Slot Images")]
    [SerializeField] private List<SlotImage> images = new List<SlotImage>();
    [SerializeField] private List<SlotImage> Tempimages = new List<SlotImage>();

    [Header("Slot Transforms")]
    [SerializeField] private Transform[] Slot_Transform = Array.Empty<Transform>();

    [Header("Miscellaneous UI")]
    [SerializeField] private TMP_Text TotalWin_text;
    [SerializeField] private TMP_Text WinLinesCount_Text;

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
    [SerializeField, Min(0.02f)] private float winSpriteFrameDuration = 0.08f;
    [SerializeField, Min(1)] private int winSymbolLoopCount = 3;
    [SerializeField] private TMP_Text phase1TotalWinText;

    [Header("Win Animation Slot Images")]
    [Tooltip("Five columns with three rows each. Assign the child Image named Animations from every Slot1 cell.")]
    [SerializeField] private WinAnimationColumn[] winAnimationColumns =
    {
        new WinAnimationColumn(),
        new WinAnimationColumn(),
        new WinAnimationColumn(),
        new WinAnimationColumn(),
        new WinAnimationColumn()
    };

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource spinAudio;
    [SerializeField] private AudioSource winAudio;

    [Header("Behaviour Events (Optional)")]
    [SerializeField] private UnityEvent onSpinStarted = new UnityEvent();
    [SerializeField] private SpinResultEvent onSpinStopped = new SpinResultEvent();
    [SerializeField] private UnityEvent onSpinRequestFailed = new UnityEvent();
    [SerializeField] private WinTierEvent onWinTier = new WinTierEvent();
    [SerializeField] private SymbolSelectedEvent onSymbolSelected = new SymbolSelectedEvent();
    [SerializeField] private UnityEvent onSymbolInfoDismissed = new UnityEvent();

    internal bool CheckPopups;

    internal event Action<SpinResult> RoundStopped;
    internal event Action<SpinResult> RequiredPresentationCompleted;
    internal event Action<string> PresentationFailed;

    internal bool IsCurrentlySpinning => IsSpinning;
    internal bool IsInitialized => isInitialized;
    internal bool IsWaitingForLateResult => waitingForLateResult;
    internal bool IsResultPresentationActive => resultPresentationInProgress;
    internal bool IsStopRequested => stopSpinRequested;
    internal bool CanBeginSpinPresentation => isInitialized && !IsSpinning &&
        !waitingForLateResult && !resultPresentationInProgress && reels.Count > 0 &&
        myImages != null && myImages.Any(sprite => sprite != null);

    private readonly List<ReelRuntime> reels = new List<ReelRuntime>();
    private readonly List<Tween> activeTweens = new List<Tween>();
    private readonly List<Tween> winTweens = new List<Tween>();
    private readonly Dictionary<Image, Sprite> animatedSymbolOriginalSprites = new Dictionary<Image, Sprite>();
    private readonly Dictionary<Image, Color> animatedSymbolOriginalColors = new Dictionary<Image, Color>();
    private readonly Dictionary<Image, Vector3> animatedSymbolOriginalScales = new Dictionary<Image, Vector3>();
    private readonly Dictionary<int, WinAnimationSlotRuntime> winAnimationSlots = new Dictionary<int, WinAnimationSlotRuntime>();
    private readonly Dictionary<int, Sprite> serverSpritesById = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, List<Sprite>> serverAnimationFramesById = new Dictionary<int, List<Sprite>>();
    private readonly List<int> mappedServerSymbolIds = new List<int>();
    private readonly HashSet<int> reportedUnknownSymbolIds = new HashSet<int>();
    private List<Sprite>[] animationSpriteArrays = Array.Empty<List<Sprite>>();
    private Transform winAnimationRoot;
    private bool serverSymbolMappingActive;
    private bool missingWinAnimationSlotWarningShown;

    internal List<List<int>> currentDisplayMatrix;

    private GameConfig gameConfig;
    private GameManager gameManager;
    private SpinResult pendingResult;
    private SpinResult lastPresentedResult;

    private Coroutine spinRoutine;
    private Coroutine winAnimationRoutine;
    private Tween winAmountTween;

    private bool gameManagerEventsBound;
    private bool isInitialized;
    private bool IsSpinning;
    private bool stopSpinRequested;
    private bool resultReceived;
    private bool resultFailed;
    private bool waitingForLateResult;
    private bool resultPresentationInProgress;
    private bool autoplayRoundInProgress;
    private bool requiredPresentationCompletionRaised;
    private bool shuttingDown;

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

    private sealed class SymbolAnimationRuntime
    {
        internal Image baseImage;
        internal Image animationImage;
        internal GameObject overlayRoot;
        internal List<Sprite> frames;
    }

    private sealed class WinAnimationSlotRuntime
    {
        internal GameObject root;
        internal Image image;
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
        BuildSymbolSpriteArrays();
        BindGameManagerEvents();
        BuildReelCache();
        BuildWinAnimationSlotCache();
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
        WinLinesCount_Text = WinLinesCount_Text != null ? WinLinesCount_Text : FindNamedText("WinLinesCount");
        phase1TotalWinText = phase1TotalWinText != null ? phase1TotalWinText : TotalWin_text;

        SetDeferredFreeSpinUiActive(false);
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

        animationSpriteArrays = new[]
        {
            BuildAnimationFrames(animSprites10, sprite10),
            BuildAnimationFrames(animSpritesA, spriteA),
            BuildAnimationFrames(animSpritesBell, spriteBell),
            BuildAnimationFrames(animSpritesCandle, spriteCandle),
            BuildAnimationFrames(animSpritesCup, spriteCup),
            BuildAnimationFrames(animSpritesDeer, spriteDeer),
            BuildAnimationFrames(animSpritesGift, spriteGift),
            BuildAnimationFrames(animSpritesJ, spriteJ),
            BuildAnimationFrames(animSpritesK, spriteK),
            BuildAnimationFrames(animSpritesMoon, spriteMoon),
            BuildAnimationFrames(animSpritesQ, spriteQ),
            BuildAnimationFrames(animSpritesSanta, spriteSanta),
            BuildAnimationFrames(animSpritesSocks, spriteSocks)
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

    private static List<Sprite> BuildAnimationFrames(List<Sprite> configuredFrames, Sprite baseSprite)
    {
        List<Sprite> frames = configuredFrames != null
            ? configuredFrames.Where(frame => frame != null).Distinct().ToList()
            : new List<Sprite>();

        if (frames.Count == 0 && baseSprite != null)
        {
            frames.Add(baseSprite);
        }

        return frames;
    }

    private bool BuildServerSymbolMapping(List<SymbolInfo> symbols)
    {
        serverSpritesById.Clear();
        serverAnimationFramesById.Clear();
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

            if (!TryResolveNamedSymbol(normalizedName, out Sprite sprite, out List<Sprite> animationFrames))
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
            serverAnimationFramesById.Add(symbol.id, BuildAnimationFrames(animationFrames, sprite));
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
        out Sprite sprite,
        out List<Sprite> animationFrames)
    {
        sprite = null;
        animationFrames = null;
        bool recognized = true;

        switch (normalizedName)
        {
            case "santa":
                sprite = spriteSanta;
                animationFrames = animSpritesSanta;
                break;
            case "gift":
            case "present":
                sprite = spriteGift;
                animationFrames = animSpritesGift;
                break;
            case "scatter":
            case "moon":
                sprite = spriteMoon;
                animationFrames = animSpritesMoon;
                break;
            case "reindeer":
            case "deer":
                sprite = spriteDeer;
                animationFrames = animSpritesDeer;
                break;
            case "bell":
            case "bells":
                sprite = spriteBell;
                animationFrames = animSpritesBell;
                break;
            case "stocking":
            case "stockings":
            case "sock":
            case "socks":
                sprite = spriteSocks;
                animationFrames = animSpritesSocks;
                break;
            case "candle":
                sprite = spriteCandle;
                animationFrames = animSpritesCandle;
                break;
            case "milkcookie":
            case "milkcookies":
            case "cookie":
            case "cookies":
            case "cup":
                sprite = spriteCup;
                animationFrames = animSpritesCup;
                break;
            case "ace":
            case "a":
                sprite = spriteA;
                animationFrames = animSpritesA;
                break;
            case "king":
            case "k":
                sprite = spriteK;
                animationFrames = animSpritesK;
                break;
            case "queen":
            case "q":
                sprite = spriteQ;
                animationFrames = animSpritesQ;
                break;
            case "jack":
            case "j":
                sprite = spriteJ;
                animationFrames = animSpritesJ;
                break;
            case "ten":
            case "10":
                sprite = sprite10;
                animationFrames = animSprites10;
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

    private void BuildWinAnimationSlotCache()
    {
        winAnimationSlots.Clear();
        missingWinAnimationSlotWarningShown = false;
        winAnimationRoot = FindWinAnimationRoot();

        int assignedSlotCount = CacheAssignedWinAnimationSlots();
        int expectedSlotCount = DefaultReelCount * DefaultRowCount;
        if (assignedSlotCount > 0 && assignedSlotCount < expectedSlotCount)
        {
            Debug.LogWarning($"[SlotBehaviour] Only {assignedSlotCount} of {expectedSlotCount} win animation Images are assigned in the Inspector. Missing positions will use the hierarchy fallback.");
        }

        if (winAnimationRoot != null)
        {
            CacheHierarchyWinAnimationSlots();
        }
        else if (assignedSlotCount == 0)
        {
            Debug.LogWarning("[SlotBehaviour] The separate win-animation slot root was not found. Win symbols will use the reel Images as a fallback.");
            return;
        }

        if (winAnimationRoot != null)
        {
            winAnimationRoot.gameObject.SetActive(false);
        }

        Debug.Log($"[SlotBehaviour] Cached {winAnimationSlots.Count} separate win-animation slots ({assignedSlotCount} assigned in the Inspector).");
    }

    private int CacheAssignedWinAnimationSlots()
    {
        if (winAnimationColumns == null || winAnimationColumns.Length == 0)
        {
            return 0;
        }

        if (winAnimationColumns.Length != DefaultReelCount)
        {
            Debug.LogWarning($"[SlotBehaviour] Win Animation Slot Images should contain {DefaultReelCount} columns, but it contains {winAnimationColumns.Length}.");
        }

        int assignedCount = 0;
        HashSet<Image> assignedImages = new HashSet<Image>();
        int columnCount = Mathf.Min(DefaultReelCount, winAnimationColumns.Length);
        for (int column = 0; column < columnCount; column++)
        {
            WinAnimationColumn configuredColumn = winAnimationColumns[column];
            if (configuredColumn?.rows == null)
            {
                continue;
            }

            if (configuredColumn.rows.Length != DefaultRowCount)
            {
                Debug.LogWarning($"[SlotBehaviour] Win animation column {column} should contain {DefaultRowCount} row Images, but it contains {configuredColumn.rows.Length}.");
            }

            int rowCount = Mathf.Min(DefaultRowCount, configuredColumn.rows.Length);
            for (int row = 0; row < rowCount; row++)
            {
                Image animationImage = configuredColumn.rows[row];
                if (animationImage == null)
                {
                    continue;
                }

                if (!assignedImages.Add(animationImage))
                {
                    Debug.LogError($"[SlotBehaviour] The same win animation Image is assigned more than once: {animationImage.name}.");
                    continue;
                }

                int flatPosition = row * DefaultReelCount + column;
                if (TryCacheWinAnimationSlot(flatPosition, animationImage, true))
                {
                    assignedCount++;
                }
            }
        }

        return assignedCount;
    }

    private void CacheHierarchyWinAnimationSlots()
    {
        int reelCount = winAnimationRoot.childCount;
        for (int column = 0; column < reelCount; column++)
        {
            Transform columnRoot = winAnimationRoot.GetChild(column);
            for (int row = 0; row < columnRoot.childCount; row++)
            {
                int flatPosition = row * reelCount + column;
                if (winAnimationSlots.ContainsKey(flatPosition))
                {
                    continue;
                }

                Transform slotRoot = columnRoot.GetChild(row);
                Transform animationTransform = slotRoot.Find("Animations");
                Image animationImage = animationTransform != null
                    ? animationTransform.GetComponent<Image>()
                    : null;
                if (animationImage == null)
                {
                    Debug.LogWarning($"[SlotBehaviour] Animation slot at column {column}, row {row} has no child Image named 'Animations'.");
                    slotRoot.gameObject.SetActive(false);
                    continue;
                }

                TryCacheWinAnimationSlot(flatPosition, animationImage, false);
            }
        }
    }

    private bool TryCacheWinAnimationSlot(int flatPosition, Image animationImage, bool inspectorAssignment)
    {
        if (animationImage == null || animationImage.transform.parent == null)
        {
            return false;
        }

        GameObject slotRoot = animationImage.transform.parent.gameObject;
        if (inspectorAssignment && animationImage.name != "Animations")
        {
            Debug.LogWarning($"[SlotBehaviour] Win animation position {flatPosition} is assigned to '{animationImage.name}'. Assign the child Image named 'Animations'.");
        }

        foreach (Image overlayImage in slotRoot.GetComponentsInChildren<Image>(true))
        {
            overlayImage.raycastTarget = false;
        }

        animationImage.gameObject.SetActive(false);
        slotRoot.SetActive(false);
        winAnimationSlots[flatPosition] = new WinAnimationSlotRuntime
        {
            root = slotRoot,
            image = animationImage
        };
        return true;
    }

    private Transform FindWinAnimationRoot()
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate =>
                candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                candidate.name == "Animations" &&
                candidate.childCount >= DefaultReelCount)
            .OrderByDescending(candidate => candidate.childCount)
            .FirstOrDefault(candidate =>
                Enumerable.Range(0, candidate.childCount)
                    .All(column => candidate.GetChild(column).childCount > 0));
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
            SetWinAmount(0d, false);
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

        ApplySantaFeatureSymbols(result);

        if (waitingForLateResult && !IsSpinning)
        {
            waitingForLateResult = false;
            PresentResult(result);
            RoundStopped?.Invoke(result);
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
        SetWinAmount(0d, false);

        spinSpeed = speed;
        IsSpinning = true;
        autoplayRoundInProgress = autoplayRound;
        stopSpinRequested = spinSpeed == SpinSpeed.QuickSpin;
        resultReceived = false;
        resultFailed = false;
        pendingResult = null;
        CheckPopups = false;

        PlayLoopingAudio(spinAudio);
        onSpinStarted?.Invoke();

        spinRoutine = StartCoroutine(SpinCoroutine());
        return true;
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
        RoundStopped?.Invoke(completedResult);
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
        SetWinAmount(displayedWin, true);

        if (displayedWin > 0d)
        {
            PlayAudio(winAudio);
            CheckWinPopups(result);
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
        double totalBet = gameManager != null ? gameManager.CurrentTotalBet : 0d;
        if (win <= 0d || totalBet <= 0d)
        {
            CheckPopups = false;
            return;
        }

        double multiplier = win / totalBet;
        int tier = multiplier >= 15d ? 3 : multiplier >= 10d ? 2 : multiplier >= 5d ? 1 : 0;
        CheckPopups = tier > 0;
        if (tier > 0)
        {
            onWinTier?.Invoke(tier, win);
        }
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
        bool loopIndividualLines = !autoplayRoundInProgress;
        requiredPresentationCompletionRaised = false;
        resultPresentationInProgress = true;
        winAnimationRoutine = StartCoroutine(PlayResultAnimations(result, loopIndividualLines));
    }

    private IEnumerator PlayResultAnimations(SpinResult result, bool loopIndividualLines)
    {
        // Let the reel-stop callback reach GameManager before a zero-duration
        // result presentation can report completion in this same frame.
        yield return null;

        HashSet<int> featurePositions = GetSantaFeaturePositions(result);
        if (featurePositions.Count > 0)
        {
            yield return AnimateSymbolPositions(featurePositions, 1, singleWinLineDuration * 0.75f);
        }

        double totalWin = result.grandTotalWin > 0d ? result.grandTotalWin : result.winAmount;
        yield return PlayTwoPhaseWinLines(result.winLines, totalWin, loopIndividualLines, CompleteRequiredResultPresentation);
        resultPresentationInProgress = false;
        autoplayRoundInProgress = false;
        winAnimationRoutine = null;
        CompleteRequiredResultPresentation();
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
        double totalWin = winLines != null ? winLines.Where(line => line != null).Sum(line => line.winAmount) : 0d;
        winAnimationRoutine = StartCoroutine(PlayTwoPhaseWinLines(winLines, totalWin, true, onComplete));
    }

    private IEnumerator PlayTwoPhaseWinLines(
        List<WinLine> winLines,
        double totalWin,
        bool loopIndividualLines,
        Action onPhaseOneComplete)
    {
        HashSet<int> allWinPositions = GetWinningPositions(winLines);
        if (allWinPositions.Count > 0)
        {
            ShowPhase1TotalWin(totalWin);
            int phaseOneLoops = autoplayRoundInProgress ? 1 : Mathf.Max(1, winSymbolLoopCount);
            yield return AnimateSymbolPositions(allWinPositions, phaseOneLoops, allWinsDuration);
        }

        onPhaseOneComplete?.Invoke();
        HidePhase1TotalWinText();

        if (!loopIndividualLines || winLines == null || winLines.Count == 0)
        {
            yield break;
        }

        while (!shuttingDown && !IsSpinning)
        {
            foreach (WinLine winLine in winLines)
            {
                if (shuttingDown || IsSpinning)
                {
                    yield break;
                }

                if (winLine?.positions != null && winLine.positions.Count > 0)
                {
                    int firstPosition = winLine.positions[0];
                    int reelCount = GetReelCount();
                    if (winLines.Count > 1)
                    {
                        ShowWinLineTextOnIcon(firstPosition % reelCount, firstPosition / reelCount, winLine.winAmount);
                    }
                    yield return AnimateSymbolPositions(
                        new HashSet<int>(winLine.positions),
                        Mathf.Max(1, winSymbolLoopCount),
                        singleWinLineDuration);
                    HideAllWinLineTexts();
                }
            }
        }
    }

    private HashSet<int> GetSantaFeaturePositions(SpinResult result)
    {
        HashSet<int> positions = new HashSet<int>();
        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : DefaultReelCount;
        int rowCount = GetRowCount();

        if (result.resultMatrix != null && gameConfig != null)
        {
            HashSet<int> wildSymbolIds = gameConfig.symbols != null
                ? new HashSet<int>(gameConfig.symbols
                    .Where(symbol => symbol != null && symbol.isWild)
                    .Select(symbol => symbol.id))
                : new HashSet<int>();

            if (wildSymbolIds.Count == 0 && gameConfig.wildSymbolIds != null)
            {
                wildSymbolIds.UnionWith(gameConfig.wildSymbolIds);
            }

            for (int column = 0; column < result.resultMatrix.Count; column++)
            {
                List<int> matrixColumn = result.resultMatrix[column];
                if (matrixColumn == null)
                {
                    continue;
                }

                for (int row = 0; row < matrixColumn.Count; row++)
                {
                    int symbolId = matrixColumn[row];
                    if (wildSymbolIds.Contains(symbolId) || symbolId == gameConfig.scatterSymbolId)
                    {
                        positions.Add(row * reelCount + column);
                    }
                }
            }
        }

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

    internal void AnimateAllScatters(int loopCount)
    {
        if (gameConfig == null || currentDisplayMatrix == null)
        {
            return;
        }

        HashSet<int> positions = GetPositionsForSymbolId(gameConfig.scatterSymbolId);
        StartOneShotSymbolAnimation(positions, loopCount, singleWinLineDuration);
    }

    internal void AnimateSantaFeatureSymbols(int loopCount = 1)
    {
        if (lastPresentedResult == null)
        {
            return;
        }

        StartOneShotSymbolAnimation(
            GetSantaFeaturePositions(lastPresentedResult),
            loopCount,
            singleWinLineDuration);
    }

    internal void AnimateSymbolSingleLoop(int column, int row, int loopCount = 1)
    {
        int reelCount = GetReelCount();
        if (column < 0 || column >= reelCount || row < 0 || row >= GetRowCount())
        {
            return;
        }

        StartOneShotSymbolAnimation(
            new HashSet<int> { row * reelCount + column },
            loopCount,
            singleWinLineDuration);
    }

    private void StartOneShotSymbolAnimation(HashSet<int> positions, int loopCount, float duration)
    {
        if (positions == null || positions.Count == 0)
        {
            return;
        }

        StopWinningAnimations();
        winAnimationRoutine = StartCoroutine(PlayOneShotSymbolAnimation(positions, loopCount, duration));
    }

    private IEnumerator PlayOneShotSymbolAnimation(HashSet<int> positions, int loopCount, float duration)
    {
        yield return AnimateSymbolPositions(positions, Mathf.Max(1, loopCount), duration);
        winAnimationRoutine = null;
    }

    private HashSet<int> GetPositionsForSymbolId(int symbolId)
    {
        HashSet<int> positions = new HashSet<int>();
        if (currentDisplayMatrix == null)
        {
            return positions;
        }

        int reelCount = GetReelCount();
        for (int column = 0; column < currentDisplayMatrix.Count; column++)
        {
            List<int> matrixColumn = currentDisplayMatrix[column];
            if (matrixColumn == null)
            {
                continue;
            }

            for (int row = 0; row < matrixColumn.Count; row++)
            {
                if (matrixColumn[row] == symbolId)
                {
                    positions.Add(row * reelCount + column);
                }
            }
        }

        return positions;
    }

    private IEnumerator AnimateSymbolPositions(HashSet<int> positions, int loopCount, float minimumDuration)
    {
        List<SymbolAnimationRuntime> activeSymbols = new List<SymbolAnimationRuntime>();
        int maxFrameCount = 1;

        foreach (int position in positions)
        {
            if (!TryGetSymbolAtPosition(position, out Image symbol, out int symbolId))
            {
                continue;
            }

            List<Sprite> frames = GetAnimationFrames(symbolId);
            if (frames == null || frames.Count == 0)
            {
                frames = BuildAnimationFrames(null, symbol.sprite);
            }

            RememberAnimatedSymbolState(symbol);
            Image animationImage = symbol;
            GameObject overlayRoot = null;
            if (TryActivateWinAnimationSlot(position, frames[0], out WinAnimationSlotRuntime animationSlot))
            {
                animationImage = animationSlot.image;
                overlayRoot = animationSlot.root;
                Color baseColor = symbol.color;
                symbol.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            }
            else if (!missingWinAnimationSlotWarningShown)
            {
                missingWinAnimationSlotWarningShown = true;
                Debug.LogWarning("[SlotBehaviour] One or more win positions have no separate animation slot; those positions will animate on the reel Image.");
            }

            activeSymbols.Add(new SymbolAnimationRuntime
            {
                baseImage = symbol,
                animationImage = animationImage,
                overlayRoot = overlayRoot,
                frames = frames
            });
            maxFrameCount = Mathf.Max(maxFrameCount, frames.Count);
        }

        if (activeSymbols.Count == 0)
        {
            yield break;
        }

        int safeLoopCount = Mathf.Max(1, loopCount);
        int totalFrameSteps = Mathf.Max(1, maxFrameCount * safeLoopCount);
        float safeDuration = Mathf.Max(0.02f, minimumDuration);
        float frameDuration = Mathf.Max(winSpriteFrameDuration, safeDuration / totalFrameSteps);
        float totalDuration = frameDuration * totalFrameSteps;
        float halfPulseDuration = totalDuration / (safeLoopCount * 2f);

        foreach (SymbolAnimationRuntime runtime in activeSymbols)
        {
            runtime.animationImage.rectTransform.localScale = Vector3.one;

            Sequence pulse = DOTween.Sequence().SetUpdate(true);
            pulse.Append(runtime.animationImage.rectTransform.DOScale(winningSymbolScale, halfPulseDuration).SetEase(Ease.OutQuad));
            pulse.Append(runtime.animationImage.rectTransform.DOScale(1f, halfPulseDuration).SetEase(Ease.InQuad));
            pulse.SetLoops(safeLoopCount, LoopType.Restart);
            winTweens.Add(pulse);
        }

        for (int frameStep = 0; frameStep < totalFrameSteps; frameStep++)
        {
            foreach (SymbolAnimationRuntime runtime in activeSymbols)
            {
                if (runtime.animationImage != null && runtime.frames.Count > 0)
                {
                    runtime.animationImage.sprite = runtime.frames[frameStep % runtime.frames.Count];
                }
            }

            yield return new WaitForSecondsRealtime(frameDuration);
        }

        foreach (SymbolAnimationRuntime runtime in activeSymbols)
        {
            runtime.animationImage?.rectTransform.DOKill();
            if (runtime.animationImage != null)
            {
                runtime.animationImage.rectTransform.localScale = Vector3.one;
            }

            if (runtime.overlayRoot != null)
            {
                runtime.overlayRoot.SetActive(false);
            }

            RestoreAnimatedSymbol(runtime.baseImage);
        }

        RefreshWinAnimationRootVisibility();
        winTweens.RemoveAll(tween => tween == null || !tween.IsActive());
    }

    private bool TryActivateWinAnimationSlot(
        int flatPosition,
        Sprite firstFrame,
        out WinAnimationSlotRuntime animationSlot)
    {
        if (!winAnimationSlots.TryGetValue(flatPosition, out animationSlot) ||
            animationSlot?.root == null || animationSlot.image == null)
        {
            return false;
        }

        if (winAnimationRoot != null)
        {
            winAnimationRoot.gameObject.SetActive(true);
        }

        animationSlot.root.SetActive(true);
        animationSlot.image.gameObject.SetActive(true);
        animationSlot.image.sprite = firstFrame;
        animationSlot.image.color = Color.white;
        animationSlot.image.rectTransform.localScale = Vector3.one;
        return true;
    }

    private void RefreshWinAnimationRootVisibility()
    {
        if (winAnimationRoot == null)
        {
            return;
        }

        bool hasActiveSlot = winAnimationSlots.Values.Any(slot => slot?.root != null && slot.root.activeSelf);
        winAnimationRoot.gameObject.SetActive(hasActiveSlot);
    }

    private void HideAllWinAnimationSlots()
    {
        foreach (WinAnimationSlotRuntime slot in winAnimationSlots.Values)
        {
            if (slot?.image != null)
            {
                slot.image.rectTransform.DOKill();
                slot.image.rectTransform.localScale = Vector3.one;
                slot.image.gameObject.SetActive(false);
            }

            if (slot?.root != null)
            {
                slot.root.SetActive(false);
            }
        }

        if (winAnimationRoot != null)
        {
            winAnimationRoot.gameObject.SetActive(false);
        }
    }

    private List<Sprite> GetAnimationFrames(int symbolId)
    {
        if (serverSymbolMappingActive)
        {
            return serverAnimationFramesById.TryGetValue(symbolId, out List<Sprite> mappedFrames)
                ? mappedFrames
                : null;
        }

        return symbolId >= 0 && symbolId < animationSpriteArrays.Length
            ? animationSpriteArrays[symbolId]
            : null;
    }

    private bool TryGetSymbolAtPosition(int flatPosition, out Image symbol, out int symbolId)
    {
        symbol = null;
        symbolId = -1;

        int reelCount = GetReelCount();
        int rowCount = GetRowCount();
        int row = flatPosition / reelCount;
        int column = flatPosition % reelCount;
        if (column < 0 || column >= reels.Count || row < 0 || row >= rowCount ||
            currentDisplayMatrix == null || column >= currentDisplayMatrix.Count ||
            currentDisplayMatrix[column] == null || row >= currentDisplayMatrix[column].Count)
        {
            return false;
        }

        ReelRuntime reel = reels[column];
        int visibleStart = Mathf.Max(0, reel.symbols.Count - rowCount);
        int imageIndex = visibleStart + row;
        if (imageIndex < 0 || imageIndex >= reel.symbols.Count)
        {
            return false;
        }

        symbol = reel.symbols[imageIndex];
        symbolId = currentDisplayMatrix[column][row];
        return symbol != null;
    }

    private void RememberAnimatedSymbolState(Image symbol)
    {
        if (symbol == null || animatedSymbolOriginalSprites.ContainsKey(symbol))
        {
            return;
        }

        animatedSymbolOriginalSprites[symbol] = symbol.sprite;
        animatedSymbolOriginalColors[symbol] = symbol.color;
        animatedSymbolOriginalScales[symbol] = symbol.rectTransform.localScale;
    }

    private void RestoreAnimatedSymbol(Image symbol)
    {
        if (symbol == null)
        {
            return;
        }

        if (animatedSymbolOriginalSprites.TryGetValue(symbol, out Sprite originalSprite))
        {
            symbol.sprite = originalSprite;
            animatedSymbolOriginalSprites.Remove(symbol);
        }

        if (animatedSymbolOriginalColors.TryGetValue(symbol, out Color originalColor))
        {
            symbol.color = originalColor;
            animatedSymbolOriginalColors.Remove(symbol);
        }

        if (animatedSymbolOriginalScales.TryGetValue(symbol, out Vector3 originalScale))
        {
            symbol.rectTransform.localScale = originalScale;
            animatedSymbolOriginalScales.Remove(symbol);
        }
    }

    private void ShowWinLineTextOnIcon(int column, int row, double winAmount)
    {
        int flatPosition = row * GetReelCount() + column;
        if (!TryGetSymbolAtPosition(flatPosition, out Image symbol, out _))
        {
            return;
        }

        Transform textTransform = symbol.transform.Find("WinLineText");
        TMP_Text winLineText = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
        if (winLineText == null)
        {
            return;
        }

        winLineText.text = FormatAmount(winAmount);
        AnimateTextScaleAppear(winLineText.transform);
    }

    private void HideAllWinLineTexts()
    {
        foreach (ReelRuntime reel in reels)
        {
            foreach (Image symbol in reel.symbols)
            {
                Transform textTransform = symbol != null ? symbol.transform.Find("WinLineText") : null;
                if (textTransform == null)
                {
                    continue;
                }

                textTransform.DOKill();
                textTransform.localScale = Vector3.one;
                textTransform.gameObject.SetActive(false);
            }
        }
    }

    private void ShowPhase1TotalWin(double totalWin)
    {
        if (phase1TotalWinText == null || totalWin <= 0d)
        {
            return;
        }

        winAmountTween?.Kill();
        phase1TotalWinText.text = FormatAmount(totalWin);
        AnimateTextScaleAppear(phase1TotalWinText.transform);
    }

    private void HidePhase1TotalWinText()
    {
        if (phase1TotalWinText == null)
        {
            return;
        }

        phase1TotalWinText.transform.DOKill();
        phase1TotalWinText.transform.localScale = Vector3.one;
        if (phase1TotalWinText != TotalWin_text)
        {
            phase1TotalWinText.gameObject.SetActive(false);
        }
    }

    private void AnimateTextScaleAppear(
        Transform textTransform,
        float popScale = 1.2f,
        float durationUp = 0.15f,
        float durationDown = 0.1f)
    {
        if (textTransform == null)
        {
            return;
        }

        textTransform.DOKill();
        textTransform.localScale = Vector3.zero;
        textTransform.gameObject.SetActive(true);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(textTransform.DOScale(popScale, durationUp).SetEase(Ease.OutQuad));
        sequence.Append(textTransform.DOScale(1f, durationDown).SetEase(Ease.InQuad));
        winTweens.Add(sequence);
    }

    private void StopWinningAnimations()
    {
        resultPresentationInProgress = false;
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
        HideAllWinAnimationSlots();
        HideAllWinLineTexts();
        HidePhase1TotalWinText();

        foreach (Image symbol in animatedSymbolOriginalSprites.Keys.ToList())
        {
            RestoreAnimatedSymbol(symbol);
        }

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

    #endregion

    #region UI and cleanup helpers

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
        winAmountTween?.Kill();
        winAmountTween = null;
    }

    #endregion
}

[Serializable]
internal sealed class WinAnimationColumn
{
    [Tooltip("Top, middle and bottom child Images named Animations.")]
    [SerializeField] internal Image[] rows = new Image[3];
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
