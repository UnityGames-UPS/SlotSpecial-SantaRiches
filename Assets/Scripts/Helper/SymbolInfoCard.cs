using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SymbolInfoCard : MonoBehaviour
{
    private const string GiftInfoText =
        "Substitutes for\nall Symbols\nexcept\nScatters";
    private const string ScatterInfoText =
        "3 or more\nScatter\nSymbols\nActivate Free\nGames";

    [Header("UI Component References")]
    [SerializeField] private Image cardBgImage;
    [SerializeField] private TMP_Text infoText;

    [Header("Pointer Sprites")]
    [Tooltip("Card shown to the right of a symbol. Its pointer should face left.")]
    [SerializeField] private Sprite rightSideCardSprite;
    [Tooltip("Optional card shown to the left of a symbol. Its pointer should face right. When omitted, the right-side sprite is mirrored safely.")]
    [SerializeField] private Sprite leftSideCardSprite;

    [Header("Layout & Auto-Close Settings")]
    [Tooltip("Horizontal distance between the symbol center and card center.")]
    [SerializeField, Min(0f)] private float xSpacing = 160f;
    [SerializeField] private float yOffset;
    [SerializeField, Min(0f)] private float autoCloseDuration = 1.5f;
    [SerializeField, Min(0f)] private float viewportPadding = 8f;

    [Header("Multiplier Text Layout")]
    [SerializeField] private float multiplierLineSpacing = 10f;

    private RectTransform rectTransform;
    private RectTransform parentRect;
    private RectTransform viewportRect;
    private RectTransform activeSymbolRect;
    private readonly Vector3[] viewportWorldCorners = new Vector3[4];
    private Vector3 baseCardScale = Vector3.one;
    private Vector3 baseTextScale = Vector3.one;
    private Vector2 baseTextAnchoredPosition;
    private float baseTextFontSize = 36f;
    private float baseTextLineSpacing;
    private TextAlignmentOptions baseTextAlignment = TextAlignmentOptions.Flush;
    private Sprite fallbackCardSprite;
    private GameManager cachedGameManager;
    private Coroutine autoCloseCoroutine;
    private int activeCol = -1;
    private int activeRow = -1;
    private int activeSymbolId = -1;
    private int activeReelCount = 5;
    private float activeCustomYOffset;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool referencesCached;

    private void Awake()
    {
        CacheReferences();
    }

    public void ShowCard(
        int symbolId,
        int colIndex,
        int rowIndex,
        RectTransform symbolRect,
        GameManager gameManager,
        float customYOffset = 0f)
    {
        CacheReferences();

        if (symbolRect == null || gameManager == null || gameManager.GameConfig == null)
        {
            Debug.LogWarning("[SymbolInfoCard] Cannot show the card because its symbol or game data is unavailable.");
            HideCard();
            return;
        }

        SymbolInfo symbolInfo = FindSymbolInfo(symbolId, gameManager.GameConfig);
        if (symbolInfo == null)
        {
            Debug.LogWarning($"[SymbolInfoCard] Symbol ID {symbolId} was not found in the current game configuration.");
            HideCard();
            return;
        }

        if (gameObject.activeSelf &&
            activeSymbolId == symbolId &&
            activeCol == colIndex &&
            activeRow == rowIndex)
        {
            HideCard();
            return;
        }

        StopAutoCloseTimer();
        activeSymbolId = symbolId;
        activeCol = colIndex;
        activeRow = rowIndex;
        activeSymbolRect = symbolRect;
        activeReelCount = Mathf.Max(1, gameManager.GameConfig.reelCount);
        activeCustomYOffset = customYOffset;
        cachedGameManager = gameManager;

        SetupCardContent(symbolInfo, gameManager);
        PositionCard(colIndex, symbolRect, activeReelCount, customYOffset);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        RestartAutoCloseTimer();
    }

    public void RefreshCard(GameManager gameManager = null)
    {
        if (!gameObject.activeSelf || activeSymbolId < 0)
        {
            return;
        }

        if (gameManager != null)
        {
            cachedGameManager = gameManager;
        }

        GameConfig config = cachedGameManager != null ? cachedGameManager.GameConfig : null;
        SymbolInfo symbolInfo = FindSymbolInfo(activeSymbolId, config);
        if (symbolInfo == null)
        {
            HideCard();
            return;
        }

        SetupCardContent(symbolInfo, cachedGameManager);
        RestartAutoCloseTimer();
    }

    public void HideCard()
    {
        StopAutoCloseTimer();
        activeCol = -1;
        activeRow = -1;
        activeSymbolId = -1;
        activeSymbolRect = null;
        cachedGameManager = null;

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (activeSymbolRect == null ||
            (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight))
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        PositionCard(activeCol, activeSymbolRect, activeReelCount, activeCustomYOffset);
    }

    private void CacheReferences()
    {
        if (referencesCached)
        {
            return;
        }

        rectTransform = transform as RectTransform;
        parentRect = rectTransform != null ? rectTransform.parent as RectTransform : null;
        Canvas canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
        viewportRect = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : parentRect;
        cardBgImage = cardBgImage != null ? cardBgImage : GetComponent<Image>();
        infoText = infoText != null ? infoText : GetComponentInChildren<TMP_Text>(true);
        fallbackCardSprite = cardBgImage != null ? cardBgImage.sprite : null;
        rightSideCardSprite = rightSideCardSprite != null ? rightSideCardSprite : fallbackCardSprite;

        if (rectTransform != null)
        {
            baseCardScale = rectTransform.localScale;
        }

        if (infoText != null)
        {
            baseTextScale = infoText.rectTransform.localScale;
            baseTextAnchoredPosition = infoText.rectTransform.anchoredPosition;
            baseTextFontSize = infoText.fontSize;
            baseTextLineSpacing = infoText.lineSpacing;
            baseTextAlignment = infoText.alignment;
        }

        referencesCached = true;
    }

    private void SetupCardContent(SymbolInfo symbolInfo, GameManager gameManager)
    {
        if (infoText == null)
        {
            return;
        }

        GameConfig config = gameManager.GameConfig;
        bool isScatter = symbolInfo.isScatter || symbolInfo.id == config.scatterSymbolId;
        bool isSanta = symbolInfo.id == config.expandingWildSymbolId;
        bool isGift = symbolInfo.id == config.giftWildSymbolId;
        bool isWild = symbolInfo.isWild ||
            (config.wildSymbolIds != null && config.wildSymbolIds.Contains(symbolInfo.id));

        if (isSanta)
        {
            SetSpecialText(GiftInfoText);
            return;
        }

        if (isGift)
        {
            SetSpecialText(GiftInfoText);
            return;
        }

        if (isScatter)
        {
            SetSpecialText(ScatterInfoText);
            return;
        }

        if (isWild)
        {
            SetSpecialText(GiftInfoText);
            return;
        }

        infoText.alignment = baseTextAlignment;
        infoText.enableAutoSizing = false;
        infoText.fontSize = baseTextFontSize;
        infoText.lineSpacing = multiplierLineSpacing;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;
        IReadOnlyList<double> payoutMultipliers = symbolInfo.multipliers;
        if (payoutMultipliers == null || payoutMultipliers.Count == 0)
        {
            infoText.text = string.Empty;
            return;
        }

        double betFactor = gameManager.CurrentBetAmount > 0d ? gameManager.CurrentBetAmount : 1d;
        var lines = new List<string>(payoutMultipliers.Count);
        for (int index = 0; index < payoutMultipliers.Count; index++)
        {
            int matchCount = ResolveMatchCount(
                symbolInfo,
                index,
                config.reelCount,
                payoutMultipliers.Count);
            double payout = payoutMultipliers[index] * betFactor;
            lines.Add(
                $"<color=#FFC700>X{matchCount}</color>     {FormatPayout(payout)}");
        }

        infoText.text = string.Join("\n", lines);
    }

    private void SetSpecialText(string value)
    {
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.enableAutoSizing = true;
        infoText.fontSizeMin = 18f;
        infoText.fontSizeMax = 36f;
        infoText.lineSpacing = baseTextLineSpacing;
        infoText.textWrappingMode = TextWrappingModes.Normal;
        infoText.text = value;
    }

    private void PositionCard(int colIndex, RectTransform symbolRect, int reelCount, float customYOffset)
    {
        if (rectTransform == null || symbolRect == null)
        {
            return;
        }

        parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, symbolRect.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                eventCamera,
                out Vector2 symbolLocalPoint))
        {
            return;
        }

        bool placeToRight = colIndex < Mathf.Max(1, reelCount / 2);
        float spacing = Mathf.Abs(xSpacing);
        Vector2 candidate = BuildCandidatePosition(symbolLocalPoint, placeToRight, spacing, customYOffset);
        if (!FitsHorizontally(candidate.x))
        {
            Vector2 oppositeCandidate = BuildCandidatePosition(symbolLocalPoint, !placeToRight, spacing, customYOffset);
            if (FitsHorizontally(oppositeCandidate.x))
            {
                placeToRight = !placeToRight;
                candidate = oppositeCandidate;
            }
        }

        ApplyPointerDirection(placeToRight);
        ClampToViewport(ref candidate);
        rectTransform.localPosition = new Vector3(candidate.x, candidate.y, rectTransform.localPosition.z);
    }

    private Vector2 BuildCandidatePosition(
        Vector2 symbolLocalPoint,
        bool placeToRight,
        float spacing,
        float customYOffset)
    {
        return new Vector2(
            symbolLocalPoint.x + (placeToRight ? spacing : -spacing),
            symbolLocalPoint.y + yOffset + customYOffset);
    }

    private bool FitsHorizontally(float centerX)
    {
        if (parentRect == null || rectTransform == null)
        {
            return true;
        }

        Rect viewportBounds = GetViewportBoundsInParentSpace();
        float halfWidth = rectTransform.rect.width * Mathf.Abs(baseCardScale.x) * 0.5f;
        return centerX - halfWidth >= viewportBounds.xMin + viewportPadding &&
               centerX + halfWidth <= viewportBounds.xMax - viewportPadding;
    }

    private void ClampToViewport(ref Vector2 position)
    {
        Rect viewportBounds = GetViewportBoundsInParentSpace();
        float halfWidth = rectTransform.rect.width * Mathf.Abs(baseCardScale.x) * 0.5f;
        float halfHeight = rectTransform.rect.height * Mathf.Abs(baseCardScale.y) * 0.5f;
        float minX = viewportBounds.xMin + halfWidth + viewportPadding;
        float maxX = viewportBounds.xMax - halfWidth - viewportPadding;
        float minY = viewportBounds.yMin + halfHeight + viewportPadding;
        float maxY = viewportBounds.yMax - halfHeight - viewportPadding;

        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : viewportBounds.center.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : viewportBounds.center.y;
    }

    private Rect GetViewportBoundsInParentSpace()
    {
        if (parentRect == null)
        {
            return default;
        }

        if (viewportRect == null)
        {
            return parentRect.rect;
        }

        viewportRect.GetWorldCorners(viewportWorldCorners);
        Vector3 firstCorner = parentRect.InverseTransformPoint(viewportWorldCorners[0]);
        float minX = firstCorner.x;
        float maxX = firstCorner.x;
        float minY = firstCorner.y;
        float maxY = firstCorner.y;

        for (int index = 1; index < viewportWorldCorners.Length; index++)
        {
            Vector3 localCorner = parentRect.InverseTransformPoint(viewportWorldCorners[index]);
            minX = Mathf.Min(minX, localCorner.x);
            maxX = Mathf.Max(maxX, localCorner.x);
            minY = Mathf.Min(minY, localCorner.y);
            maxY = Mathf.Max(maxY, localCorner.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void ApplyPointerDirection(bool cardIsToRightOfSymbol)
    {
        bool mirrorCard = !cardIsToRightOfSymbol && leftSideCardSprite == null;
        if (cardBgImage != null)
        {
            cardBgImage.sprite = cardIsToRightOfSymbol
                ? rightSideCardSprite ?? fallbackCardSprite
                : leftSideCardSprite ?? rightSideCardSprite ?? fallbackCardSprite;
        }

        rectTransform.localScale = new Vector3(
            Mathf.Abs(baseCardScale.x) * (mirrorCard ? -1f : 1f),
            baseCardScale.y,
            baseCardScale.z);

        if (infoText == null)
        {
            return;
        }

        RectTransform textRect = infoText.rectTransform;
        textRect.localScale = new Vector3(
            Mathf.Abs(baseTextScale.x) * (mirrorCard ? -1f : 1f),
            baseTextScale.y,
            baseTextScale.z);
        textRect.anchoredPosition = new Vector2(
            baseTextAnchoredPosition.x * (mirrorCard ? -1f : 1f),
            baseTextAnchoredPosition.y);
    }

    private void RestartAutoCloseTimer()
    {
        StopAutoCloseTimer();
        if (autoCloseDuration > 0f && gameObject.activeInHierarchy)
        {
            autoCloseCoroutine = StartCoroutine(AutoCloseTimer());
        }
    }

    private IEnumerator AutoCloseTimer()
    {
        yield return new WaitForSecondsRealtime(autoCloseDuration);
        autoCloseCoroutine = null;
        HideCard();
    }

    private void StopAutoCloseTimer()
    {
        if (autoCloseCoroutine == null)
        {
            return;
        }

        StopCoroutine(autoCloseCoroutine);
        autoCloseCoroutine = null;
    }

    private void OnDisable()
    {
        StopAutoCloseTimer();
    }

    private static SymbolInfo FindSymbolInfo(int symbolId, GameConfig config)
    {
        return config?.symbols?.Find(symbol => symbol != null && symbol.id == symbolId);
    }

    private static int ResolveMatchCount(
        SymbolInfo symbolInfo,
        int index,
        int reelCount,
        int payoutCount)
    {
        if (symbolInfo.matchCounts != null &&
            symbolInfo.matchCounts.Count == payoutCount &&
            index < symbolInfo.matchCounts.Count)
        {
            return symbolInfo.matchCounts[index];
        }

        int safeReelCount = Mathf.Max(1, reelCount);
        int minimumMatch = symbolInfo.minMatch > 0 ? symbolInfo.minMatch : 1;
        return Mathf.Max(minimumMatch, safeReelCount - index);
    }

    private static string FormatPayout(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "0.00";
        }

        return value.ToString("0.00##############", CultureInfo.InvariantCulture);
    }
}
