using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class OCController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OrientationChange orientationChange;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Transform slotObject;
    [SerializeField] private List<RectTransform> resizedObjects = new List<RectTransform>();
    [SerializeField] private List<RectTransform> squareResizedObjects = new List<RectTransform>();

    [Header("Panel Toggle Settings")]
    [SerializeField] private GameObject landscapePanelObject;
    [SerializeField] private GameObject portraitPanelObject;

    [Header("Background Toggle Settings")]
    [SerializeField] private GameObject landscapeBackground;
    [SerializeField] private GameObject portraitBackground;

    [Header("Canvas Scaler Resolutions")]
    [SerializeField] private Vector2 landscapeReferenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitReferenceResolution = new Vector2(1080f, 1920f);

    [Header("Resized Object Dimensions")]
    [SerializeField] private Vector2 landscapeResizedObjectSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitResizedObjectSize = new Vector2(1080f, 1920f);

    [Header("Square Resized Object Dimensions")]
    [SerializeField] private Vector2 landscapeSquareResizedObjectSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitSquareResizedObjectSize = new Vector2(1920f, 1920f);

    [Header("Slot Object Settings")]
    [SerializeField] private Vector3 landscapeSlotScale = Vector3.one;
    [SerializeField] private Vector3 portraitSlotScale = new Vector3(0.73f, 0.73f, 0.73f);
    [SerializeField] private Vector3 landscapeSlotPosition = Vector3.zero;
    [SerializeField] private Vector3 portraitSlotPosition = new Vector3(0f, -150f, 0f);

    [Header("Slot Overlay Settings")]
    [SerializeField] private RectTransform freeSpinPanel;
    [SerializeField] private Vector2 portraitFreeSpinPanelPosition = Vector2.zero;
    [SerializeField] private Vector2 portraitFreeSpinPanelSize = new Vector2(2000f, 3500f);
    [SerializeField] private Vector3 portraitFreeSpinPanelScale = Vector3.one;
    [SerializeField] private RectTransform freeSpinWinPanel;
    [SerializeField] private Vector2 portraitFreeSpinWinPanelPosition = Vector2.zero;
    [SerializeField] private Vector2 portraitFreeSpinWinPanelSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector3 portraitFreeSpinWinPanelScale = Vector3.one;

    [Header("Free Spin Win Background Settings")]
    [SerializeField] private Image freeSpinWinBackgroundImage;
    [SerializeField] private Sprite landscapeFreeSpinWinBackground;
    [SerializeField] private Sprite portraitFreeSpinWinBackground;

    [Header("Santa Object Settings")]
    [SerializeField] private RectTransform santaObject;
    [SerializeField] private RectTransform slotHolderObject;
    [SerializeField] private Vector2 portraitSantaPosition = new Vector2(28f, 568f);
    [SerializeField] private Vector2 portraitSantaSize = new Vector2(900f, 1000f);
    [SerializeField] private Vector3 portraitSantaScale = Vector3.one;

    [Header("Info Page & Guide Layout")]
    [FormerlySerializedAs("infoPageScrollObject")]
    [SerializeField] private RectTransform infoPageLayoutObject;
    [FormerlySerializedAs("guideScrollObject")]
    [SerializeField] private RectTransform guidePageLayoutObject;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.2f;

    private List<Tween> activeTweens = new List<Tween>();
    private bool usesInstanceOrientationEvent;
    private Vector2 landscapeSantaPosition;
    private Vector2 landscapeSantaSize;
    private Vector3 landscapeSantaScale;
    private bool hasCachedLandscapeSantaState;
    private Sprite defaultFreeSpinWinBackground;
    private readonly Dictionary<RectTransform, RectTransformState> landscapeSlotOverlayStates =
        new Dictionary<RectTransform, RectTransformState>();

    private struct RectTransformState
    {
        public Vector2 Position;
        public Vector2 Size;
        public Vector3 Scale;

        public RectTransformState(RectTransform rectTransform)
        {
            Position = rectTransform.anchoredPosition;
            Size = rectTransform.sizeDelta;
            Scale = rectTransform.localScale;
        }
    }

    private void Awake()
    {
        if (orientationChange == null)
        {
            orientationChange = GetComponent<OrientationChange>();
            if (orientationChange == null)
            {
                orientationChange = Object.FindFirstObjectByType<OrientationChange>();
            }
        }
        if (canvasScaler == null && orientationChange != null)
        {
            canvasScaler = orientationChange.GetComponent<CanvasScaler>();
        }
        if (santaObject == null && slotObject != null)
        {
            santaObject = slotObject.Find("Santa") as RectTransform;
        }
        if (slotHolderObject == null && slotObject != null)
        {
            slotHolderObject = slotObject.Find("SlotHolder") as RectTransform;
        }

        ResolvePageLayoutObjects();
        EnsureSantaPresentationParent();
        CacheLandscapeSantaState();
        CacheLandscapeSlotOverlayStates();
        ResolveFreeSpinWinBackground();
    }

    private void OnEnable()
    {
        if (orientationChange != null)
        {
            orientationChange.OnOrientationChangedInstance += HandleOrientationChange;
            usesInstanceOrientationEvent = true;
        }
        else
        {
            OrientationChange.OnOrientationChanged += HandleOrientationChange;
            usesInstanceOrientationEvent = false;
        }
    }

    private void OnDisable()
    {
        if (usesInstanceOrientationEvent && orientationChange != null)
        {
            orientationChange.OnOrientationChangedInstance -= HandleOrientationChange;
        }
        else
        {
            OrientationChange.OnOrientationChanged -= HandleOrientationChange;
        }

        KillActiveTweens();
    }

    private void HandleOrientationChange(OrientationChange.OrientationMode mode, int width, int height)
    {
        KillActiveTweens();

        bool isMobilePortrait = (mode == OrientationChange.OrientationMode.MobilePortrait);

        // 1. Toggle Landscape vs Portrait Panel Objects
        if (landscapePanelObject != null)
        {
            landscapePanelObject.SetActive(!isMobilePortrait);
        }
        if (portraitPanelObject != null)
        {
            portraitPanelObject.SetActive(isMobilePortrait);
        }

        // 2. Toggle Landscape vs Portrait Background Objects
        if (landscapeBackground != null)
        {
            landscapeBackground.SetActive(!isMobilePortrait);
        }
        if (portraitBackground != null)
        {
            portraitBackground.SetActive(isMobilePortrait);
        }

        // 3. Update Canvas Scaler Reference Resolution
        if (canvasScaler != null)
        {
            Vector2 targetRefRes = isMobilePortrait ? portraitReferenceResolution : landscapeReferenceResolution;
            canvasScaler.referenceResolution = targetRefRes;
        }

        // 4. Resize Target RectTransforms
        Vector2 targetSize = isMobilePortrait ? portraitResizedObjectSize : landscapeResizedObjectSize;
        if (resizedObjects != null)
        {
            foreach (var rect in resizedObjects)
            {
                if (rect != null)
                {
                    if (transitionDuration > 0)
                    {
                        Tween t = rect.DOSizeDelta(targetSize, transitionDuration).SetEase(Ease.OutCubic);
                        activeTweens.Add(t);
                    }
                    else
                    {
                        rect.sizeDelta = targetSize;
                    }
                }
            }
        }

        // 4b. Resize Target RectTransforms (1920x1080 Landscape, 1920x1920 Portrait)
        Vector2 targetSquareSize = isMobilePortrait ? portraitSquareResizedObjectSize : landscapeSquareResizedObjectSize;
        if (squareResizedObjects != null)
        {
            foreach (var rect in squareResizedObjects)
            {
                if (rect != null)
                {
                    if (transitionDuration > 0)
                    {
                        Tween t = rect.DOSizeDelta(targetSquareSize, transitionDuration).SetEase(Ease.OutCubic);
                        activeTweens.Add(t);
                    }
                    else
                    {
                        rect.sizeDelta = targetSquareSize;
                    }
                }
            }
        }

        // 5. Update Slot Object Scale and Position
        if (slotObject != null)
        {
            Vector3 targetScale = isMobilePortrait ? portraitSlotScale : landscapeSlotScale;
            Vector3 targetPosition = isMobilePortrait ? portraitSlotPosition : landscapeSlotPosition;

            if (transitionDuration > 0)
            {
                Tween scaleTween = slotObject.DOScale(targetScale, transitionDuration).SetEase(Ease.OutCubic);
                Tween posTween = slotObject.DOLocalMove(targetPosition, transitionDuration).SetEase(Ease.OutCubic);
                activeTweens.Add(scaleTween);
                activeTweens.Add(posTween);
            }
            else
            {
                slotObject.localScale = targetScale;
                slotObject.localPosition = targetPosition;
            }
        }

        // 6. Apply independent portrait layouts to the free-spin overlays.
        UpdateSlotOverlayLayouts(isMobilePortrait);
        UpdateFreeSpinWinBackground(isMobilePortrait);

        // 7. Apply portrait-only Santa settings, or restore its saved landscape state.
        UpdateSantaLayout(isMobilePortrait);

        // 8. Update Info Page Height (1080 for Landscape, 1920 for Mobile Portrait)
        if (infoPageLayoutObject != null)
        {
            float targetHeight = isMobilePortrait ? 1920f : 1080f;
            Vector2 targetScrollSize = new Vector2(infoPageLayoutObject.sizeDelta.x, targetHeight);
            if (transitionDuration > 0)
            {
                Tween scrollTween = infoPageLayoutObject.DOSizeDelta(targetScrollSize, transitionDuration).SetEase(Ease.OutCubic);
                activeTweens.Add(scrollTween);
            }
            else
            {
                infoPageLayoutObject.sizeDelta = targetScrollSize;
            }
        }

        // 9. Update Guide Page Height (1080 for Landscape, 1920 for Mobile Portrait)
        if (guidePageLayoutObject != null)
        {
            float targetHeight = isMobilePortrait ? 1920f : 1080f;
            Vector2 targetScrollSize = new Vector2(guidePageLayoutObject.sizeDelta.x, targetHeight);
            if (transitionDuration > 0)
            {
                Tween scrollTween = guidePageLayoutObject.DOSizeDelta(targetScrollSize, transitionDuration).SetEase(Ease.OutCubic);
                activeTweens.Add(scrollTween);
            }
            else
            {
                guidePageLayoutObject.sizeDelta = targetScrollSize;
            }
        }
    }

    private void ResolvePageLayoutObjects()
    {
        infoPageLayoutObject = ResolvePageLayoutObject(infoPageLayoutObject, "InfoScroll");
        guidePageLayoutObject = ResolvePageLayoutObject(guidePageLayoutObject, "GuideScroll");
    }

    private static RectTransform ResolvePageLayoutObject(RectTransform assignedObject, string scrollObjectName)
    {
        if (assignedObject == null)
        {
            return null;
        }

        ScrollRect[] scrollRects = assignedObject.GetComponentsInChildren<ScrollRect>(true);
        foreach (ScrollRect scrollRect in scrollRects)
        {
            if (scrollRect != null && scrollRect.name == scrollObjectName)
            {
                // The page-sized object is the ScrollRect's parent. This also
                // corrects an Inspector reference that points at the outer panel.
                return scrollRect.transform.parent as RectTransform ?? assignedObject;
            }
        }

        return assignedObject;
    }

    private void KillActiveTweens()
    {
        foreach (var t in activeTweens)
        {
            if (t != null && t.IsActive())
            {
                t.Kill();
            }
        }
        activeTweens.Clear();
    }

    private void CacheLandscapeSantaState()
    {
        if (santaObject == null || hasCachedLandscapeSantaState)
        {
            return;
        }

        landscapeSantaPosition = santaObject.anchoredPosition;
        landscapeSantaSize = santaObject.sizeDelta;
        landscapeSantaScale = santaObject.localScale;
        hasCachedLandscapeSantaState = true;
    }

    private void CacheLandscapeSlotOverlayStates()
    {
        CacheLandscapeSlotOverlayState(freeSpinPanel);
        CacheLandscapeSlotOverlayState(freeSpinWinPanel);
    }

    private void ResolveFreeSpinWinBackground()
    {
        if (freeSpinWinBackgroundImage == null && freeSpinWinPanel != null)
        {
            freeSpinWinBackgroundImage = freeSpinWinPanel.GetComponent<Image>();
        }

        if (freeSpinWinBackgroundImage != null && defaultFreeSpinWinBackground == null)
        {
            defaultFreeSpinWinBackground = freeSpinWinBackgroundImage.sprite;
        }
    }

    private void UpdateFreeSpinWinBackground(bool isMobilePortrait)
    {
        ResolveFreeSpinWinBackground();
        if (freeSpinWinBackgroundImage == null)
        {
            return;
        }

        Sprite targetBackground = isMobilePortrait
            ? portraitFreeSpinWinBackground ?? landscapeFreeSpinWinBackground ?? defaultFreeSpinWinBackground
            : landscapeFreeSpinWinBackground ?? defaultFreeSpinWinBackground;

        if (targetBackground != null)
        {
            freeSpinWinBackgroundImage.sprite = targetBackground;
        }
    }

    private void CacheLandscapeSlotOverlayState(RectTransform overlay)
    {
        if (overlay != null && !landscapeSlotOverlayStates.ContainsKey(overlay))
        {
            landscapeSlotOverlayStates.Add(overlay, new RectTransformState(overlay));
        }
    }

    private void UpdateSlotOverlayLayouts(bool isMobilePortrait)
    {
        CacheLandscapeSlotOverlayStates();

        UpdateSlotOverlayLayout(
            freeSpinPanel,
            isMobilePortrait,
            portraitFreeSpinPanelPosition,
            portraitFreeSpinPanelSize,
            portraitFreeSpinPanelScale);

        UpdateSlotOverlayLayout(
            freeSpinWinPanel,
            isMobilePortrait,
            portraitFreeSpinWinPanelPosition,
            portraitFreeSpinWinPanelSize,
            portraitFreeSpinWinPanelScale);
    }

    private void UpdateSlotOverlayLayout(
        RectTransform overlay,
        bool isMobilePortrait,
        Vector2 portraitPosition,
        Vector2 portraitSize,
        Vector3 portraitScale)
    {
        if (overlay == null || !landscapeSlotOverlayStates.TryGetValue(overlay, out RectTransformState landscapeState))
        {
            return;
        }

        Vector2 targetPosition = isMobilePortrait ? portraitPosition : landscapeState.Position;
        Vector2 targetSize = isMobilePortrait ? portraitSize : landscapeState.Size;
        Vector3 targetScale = isMobilePortrait ? portraitScale : landscapeState.Scale;

        if (transitionDuration > 0f)
        {
            activeTweens.Add(overlay.DOAnchorPos(targetPosition, transitionDuration).SetEase(Ease.OutCubic));
            activeTweens.Add(overlay.DOSizeDelta(targetSize, transitionDuration).SetEase(Ease.OutCubic));
            activeTweens.Add(overlay.DOScale(targetScale, transitionDuration).SetEase(Ease.OutCubic));
        }
        else
        {
            overlay.anchoredPosition = targetPosition;
            overlay.sizeDelta = targetSize;
            overlay.localScale = targetScale;
        }
    }

    private void UpdateSantaLayout(bool isMobilePortrait)
    {
        if (santaObject == null)
        {
            return;
        }

        CacheLandscapeSantaState();
        UpdateSantaDrawOrder(isMobilePortrait);

        Vector2 targetPosition = isMobilePortrait ? portraitSantaPosition : landscapeSantaPosition;
        Vector2 targetSize = isMobilePortrait ? portraitSantaSize : landscapeSantaSize;
        Vector3 targetScale = isMobilePortrait ? portraitSantaScale : landscapeSantaScale;

        if (transitionDuration > 0f)
        {
            activeTweens.Add(santaObject.DOAnchorPos(targetPosition, transitionDuration).SetEase(Ease.OutCubic));
            activeTweens.Add(santaObject.DOSizeDelta(targetSize, transitionDuration).SetEase(Ease.OutCubic));
            activeTweens.Add(santaObject.DOScale(targetScale, transitionDuration).SetEase(Ease.OutCubic));
        }
        else
        {
            santaObject.anchoredPosition = targetPosition;
            santaObject.sizeDelta = targetSize;
            santaObject.localScale = targetScale;
        }

    }

    private void EnsureSantaPresentationParent()
    {
        if (santaObject == null || slotObject == null || santaObject.parent == slotObject)
        {
            return;
        }

        santaObject.SetParent(slotObject, false);
    }

    private void UpdateSantaDrawOrder(bool isMobilePortrait)
    {
        if (santaObject == null || slotObject == null || slotHolderObject == null)
        {
            return;
        }

        EnsureSantaPresentationParent();
        if (slotHolderObject.parent != slotObject)
        {
            return;
        }

        int santaIndex = santaObject.GetSiblingIndex();
        int slotHolderIndex = slotHolderObject.GetSiblingIndex();

        if (isMobilePortrait)
        {
            // Draw Santa behind the complete SlotHolder subtree in portrait.
            int portraitIndex = santaIndex < slotHolderIndex
                ? slotHolderIndex - 1
                : slotHolderIndex;
            santaObject.SetSiblingIndex(portraitIndex);
        }
        else
        {
            // Draw Santa above the complete SlotHolder subtree in landscape.
            int landscapeIndex = santaIndex < slotHolderIndex
                ? slotHolderIndex
                : slotHolderIndex + 1;
            santaObject.SetSiblingIndex(landscapeIndex);
        }
    }
}
