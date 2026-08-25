using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ExtraGiftWildController : MonoBehaviour
{
    private const float ImageAnimationBaseFrameDuration = 0.0416666679f;
    private const int MaximumGiftsPerAnimationCycle = 2;

    [Header("Santa Movement")]
    [SerializeField] private RectTransform movingRect;
    [SerializeField] private ImageAnimation santaAnimation;
    [SerializeField] private float leftX = -1500f;
    [SerializeField] private float rightX = 1500f;
    [Tooltip("Time for Santa to cross continuously from left to right.")]
    [SerializeField, Min(0.01f)] private float duration = 5f;

    [Header("Scene Santa")]
    [SerializeField] private RectTransform sceneSanta;
    [SerializeField] private float sceneSantaExitOffsetX = -700f;
    [SerializeField, Min(0.01f)] private float sceneSantaMoveDuration = 0.75f;

    [Header("Flying Gift")]
    [SerializeField] private RectTransform giftParent;
    [SerializeField] private Vector2 handOffset = new Vector2(-100f, 24f);
    [SerializeField] private Vector2 giftSize = new Vector2(220f, 220f);
    [SerializeField, Min(0.01f)] private float giftTravelDuration = 1.25f;
    [SerializeField, Min(0f)] private float giftStartScale;
    [SerializeField, Min(0f)] private float giftArcHeight = 260f;
    [SerializeField] private float giftRotationDegrees = 360f;

    [Header("Presentation Overlay")]
    [SerializeField] private RectTransform blackScreen;

    private readonly List<RectTransform> activeGiftProjectiles = new List<RectTransform>();
    private Tween santaMovementTween;
    private Tween sceneSantaTween;
    private Vector2 sceneSantaHomePosition;
    private bool hasSceneSantaHomePosition;
    private int activeGiftFlights;
    private bool isPresenting;

    internal bool CanPresent
    {
        get
        {
            ResolveReferences();
            return movingRect != null;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        StopSantaAnimation();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        KillPresentationTweens();
        StopSantaAnimation();
        SetBlackScreenActive(false);
        activeGiftFlights = 0;
        isPresenting = false;
        SetSantaX(leftX);
        RestoreSceneSantaImmediately();
    }

    internal IEnumerator PlayPresentation(
        IReadOnlyList<RectTransform> targets,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation,
        Action onSleighExited)
    {
        ResolveReferences();
        if (movingRect == null || targets == null || targets.Count == 0)
        {
            yield break;
        }

        StopAllCoroutines();
        KillPresentationTweens();
        activeGiftFlights = 0;
        isPresenting = true;
        SetSantaX(leftX);
        StopSantaAnimation();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        SetBlackScreenActive(false);
        bool moveSceneSanta = !IsPortraitMode();

        Tween santaExitTween = moveSceneSanta
            ? MoveSceneSantaTo(
                sceneSantaHomePosition + Vector2.right * sceneSantaExitOffsetX,
                Ease.InQuad)
            : null;
        if (santaExitTween != null)
        {
            yield return santaExitTween.WaitForCompletion();
        }
        sceneSantaTween = null;

        if (!isPresenting)
        {
            yield break;
        }

        SetBlackScreenActive(true);
        float frameDelay = GetSantaFrameDelay();
        float animationCycleDuration = frameDelay > 0f && santaAnimation?.textureArray != null
            ? frameDelay * santaAnimation.textureArray.Count
            : 0f;
        int animationCycleCount = Mathf.CeilToInt(
            targets.Count / (float)MaximumGiftsPerAnimationCycle);
        float requiredBatchDuration = animationCycleDuration > 0f
            ? animationCycleDuration * animationCycleCount + frameDelay
            : 0f;
        float crossingDuration = Mathf.Max(
            Mathf.Max(0.01f, duration),
            requiredBatchDuration);
        StartSantaCrossing(crossingDuration);

        yield return AnimateSantaAndReleaseGifts(
            targets,
            giftSprite,
            playLandingAnimation,
            crossingDuration,
            frameDelay);

        while (isPresenting && activeGiftFlights > 0)
        {
            yield return null;
        }

        if (isPresenting)
        {
            onSleighExited?.Invoke();
            SetBlackScreenActive(false);
            if (moveSceneSanta)
            {
                yield return MoveSceneSantaHome();
            }
            FinishPresentation();
        }
    }

    internal void StopPresentation()
    {
        isPresenting = false;
        StopAllCoroutines();
        KillPresentationTweens();
        activeGiftFlights = 0;
        StopSantaAnimation();
        SetSantaX(leftX);
        RestoreSceneSantaImmediately();
        SetBlackScreenActive(false);
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void StartSantaCrossing(float crossingDuration)
    {
        santaMovementTween?.Kill();
        santaMovementTween = movingRect
            .DOAnchorPosX(rightX, Mathf.Max(0.01f, crossingDuration))
            .SetEase(Ease.Linear)
            .SetUpdate(true);
    }

    private IEnumerator AnimateSantaAndReleaseGifts(
        IReadOnlyList<RectTransform> targets,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation,
        float crossingDuration,
        float frameDelay)
    {
        if (santaAnimation == null ||
            santaAnimation.rendererDelegate == null ||
            santaAnimation.textureArray == null ||
            santaAnimation.textureArray.Count == 0 ||
            frameDelay <= 0f)
        {
            yield return ReleaseGiftsWithoutSantaFrames(
                targets,
                giftSprite,
                playLandingAnimation,
                crossingDuration);
            yield break;
        }

        santaAnimation.StopAnimation();
        List<Sprite> frames = santaAnimation.textureArray;
        WaitForSecondsRealtime waitForFrame = new WaitForSecondsRealtime(frameDelay);
        int frameIndex = 0;
        int nextTargetIndex = 0;

        while (isPresenting && IsSantaCrossing())
        {
            if (frames[frameIndex] != null)
            {
                santaAnimation.rendererDelegate.sprite = frames[frameIndex];
            }

            yield return waitForFrame;

            frameIndex++;
            if (frameIndex < frames.Count)
            {
                continue;
            }

            frameIndex = 0;
            if (nextTargetIndex < targets.Count)
            {
                nextTargetIndex = StartGiftFlightBatch(
                    nextTargetIndex,
                    targets,
                    giftSprite,
                    playLandingAnimation);
            }
        }

        // The calculated crossing time gives every two-gift batch a complete
        // Santa animation cycle. Keep a defensive fallback so no server gift is
        // dropped if the animation timing is edited at runtime.
        if (isPresenting && nextTargetIndex < targets.Count)
        {
            StartGiftFlightBatch(
                nextTargetIndex,
                targets,
                giftSprite,
                playLandingAnimation);
        }

        santaMovementTween = null;
    }

    private IEnumerator ReleaseGiftsWithoutSantaFrames(
        IReadOnlyList<RectTransform> targets,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation,
        float crossingDuration)
    {
        int batchCount = Mathf.CeilToInt(
            targets.Count / (float)MaximumGiftsPerAnimationCycle);
        float batchDelay = crossingDuration / Mathf.Max(1, batchCount + 1);
        int nextTargetIndex = 0;
        while (isPresenting && nextTargetIndex < targets.Count)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, batchDelay));
            nextTargetIndex = StartGiftFlightBatch(
                nextTargetIndex,
                targets,
                giftSprite,
                playLandingAnimation);
        }

        while (isPresenting && IsSantaCrossing())
        {
            yield return null;
        }

        santaMovementTween = null;
    }

    private int StartGiftFlightBatch(
        int startTargetIndex,
        IReadOnlyList<RectTransform> targets,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation)
    {
        int endTargetIndex = Mathf.Min(
            startTargetIndex + MaximumGiftsPerAnimationCycle,
            targets.Count);
        for (int targetIndex = startTargetIndex;
             targetIndex < endTargetIndex;
             targetIndex++)
        {
            StartGiftFlight(
                targetIndex,
                targets[targetIndex],
                giftSprite,
                playLandingAnimation);
        }

        return endTargetIndex;
    }

    private void StartGiftFlight(
        int targetIndex,
        RectTransform target,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation)
    {
        activeGiftFlights++;
        StartCoroutine(FlyGiftAndPlayLanding(
            targetIndex,
            target,
            giftSprite,
            playLandingAnimation));
    }

    private IEnumerator FlyGiftAndPlayLanding(
        int targetIndex,
        RectTransform target,
        Sprite giftSprite,
        Func<int, IEnumerator> playLandingAnimation)
    {
        if (target != null && giftSprite != null)
        {
            yield return FlyGiftToTarget(target, giftSprite, targetIndex);
        }

        if (isPresenting && playLandingAnimation != null)
        {
            IEnumerator landingAnimation = playLandingAnimation(targetIndex);
            if (landingAnimation != null)
            {
                yield return landingAnimation;
            }
        }

        activeGiftFlights = Mathf.Max(0, activeGiftFlights - 1);
    }

    private IEnumerator FlyGiftToTarget(
        RectTransform target,
        Sprite giftSprite,
        int targetIndex)
    {
        RectTransform parent = giftParent != null
            ? giftParent
            : movingRect.parent as RectTransform;
        if (parent == null)
        {
            yield break;
        }

        GameObject projectileObject = new GameObject(
            "Extra Gift Projectile",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform projectile = projectileObject.GetComponent<RectTransform>();
        projectile.SetParent(parent, false);
        projectile.SetAsLastSibling();
        projectile.sizeDelta = giftSize;
        projectile.position = movingRect.TransformPoint(handOffset);
        projectile.localScale = Vector3.one * giftStartScale;

        Image projectileImage = projectileObject.GetComponent<Image>();
        projectileImage.sprite = giftSprite;
        projectileImage.preserveAspect = true;
        projectileImage.raycastTarget = false;

        activeGiftProjectiles.Add(projectile);
        float travelDuration = Mathf.Max(0.01f, giftTravelDuration);
        Vector3 startPosition = projectile.position;
        Vector3 destination = target.position;
        float sideDirection = Mathf.Sign(destination.x - startPosition.x);
        if (Mathf.Approximately(sideDirection, 0f))
        {
            sideDirection = targetIndex % 2 == 0 ? -1f : 1f;
        }

        float pathProgress = 0f;
        Sequence flight = DOTween.Sequence()
            .SetTarget(projectile)
            .SetUpdate(true);
        flight.Append(DOTween
            .To(
                () => pathProgress,
                value =>
                {
                    pathProgress = value;
                    projectile.position = EvaluateSingleArc(
                        startPosition,
                        destination,
                        value,
                        giftArcHeight);
                },
                1f,
                travelDuration)
            .SetEase(Ease.Linear));
        flight.Insert(0f, projectile
            .DOScale(Vector3.one, travelDuration)
            .SetEase(Ease.OutCubic));
        flight.Insert(0f, projectile
            .DORotate(
                new Vector3(0f, 0f, -giftRotationDegrees * sideDirection),
                travelDuration,
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear));

        yield return flight.WaitForCompletion();

        if (projectile != null)
        {
            projectile.position = destination;
            projectile.localScale = Vector3.one;
        }

        activeGiftProjectiles.Remove(projectile);
        if (projectileObject != null)
        {
            Destroy(projectileObject);
        }
    }

    private static Vector3 EvaluateSingleArc(
        Vector3 start,
        Vector3 end,
        float progress,
        float height)
    {
        float t = Mathf.Clamp01(progress);
        Vector3 directPosition = Vector3.LerpUnclamped(start, end, t);
        float circularLift = Mathf.Sin(Mathf.PI * t) * Mathf.Max(0f, height);
        return directPosition + Vector3.up * circularLift;
    }

    private float GetSantaFrameDelay()
    {
        if (santaAnimation?.textureArray == null ||
            santaAnimation.textureArray.Count == 0)
        {
            return 0f;
        }

        return ImageAnimationBaseFrameDuration * santaAnimation.textureArray.Count /
            Mathf.Max(0.01f, santaAnimation.AnimationSpeed);
    }

    private bool IsSantaCrossing()
    {
        return santaMovementTween != null &&
            santaMovementTween.IsActive() &&
            !santaMovementTween.IsComplete();
    }

    private void FinishPresentation()
    {
        isPresenting = false;
        KillPresentationTweens();
        StopSantaAnimation();
        SetSantaX(leftX);
        SetBlackScreenActive(false);
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void KillPresentationTweens()
    {
        santaMovementTween?.Kill();
        santaMovementTween = null;
        sceneSantaTween?.Kill();
        sceneSantaTween = null;

        for (int index = activeGiftProjectiles.Count - 1; index >= 0; index--)
        {
            RectTransform projectile = activeGiftProjectiles[index];
            if (projectile == null)
            {
                continue;
            }

            projectile.DOKill();
            Destroy(projectile.gameObject);
        }

        activeGiftProjectiles.Clear();
    }

    private void StopSantaAnimation()
    {
        santaAnimation?.StopAnimation();
        ResetSantaAnimation();
    }

    private void ResetSantaAnimation()
    {
        if (santaAnimation?.rendererDelegate == null ||
            santaAnimation.textureArray == null ||
            santaAnimation.textureArray.Count == 0)
        {
            return;
        }

        Sprite firstFrame = santaAnimation.textureArray.FirstOrDefault(frame => frame != null);
        if (firstFrame != null)
        {
            santaAnimation.rendererDelegate.sprite = firstFrame;
        }
    }

    private void SetSantaX(float x)
    {
        if (movingRect == null)
        {
            return;
        }

        Vector2 position = movingRect.anchoredPosition;
        position.x = x;
        movingRect.anchoredPosition = position;
    }

    private Tween MoveSceneSantaTo(Vector2 targetPosition, Ease ease)
    {
        if (sceneSanta == null || !hasSceneSantaHomePosition)
        {
            return null;
        }

        sceneSantaTween?.Kill();
        sceneSantaTween = sceneSanta
            .DOAnchorPos(targetPosition, Mathf.Max(0.01f, sceneSantaMoveDuration))
            .SetEase(ease)
            .SetUpdate(true);
        return sceneSantaTween;
    }

    private IEnumerator MoveSceneSantaHome()
    {
        Tween returnTween = MoveSceneSantaTo(sceneSantaHomePosition, Ease.OutQuad);
        if (returnTween != null)
        {
            yield return returnTween.WaitForCompletion();
        }

        sceneSantaTween = null;
    }

    private void RestoreSceneSantaImmediately()
    {
        if (sceneSanta == null || !hasSceneSantaHomePosition)
        {
            return;
        }

        sceneSantaTween?.Kill();
        sceneSantaTween = null;
        sceneSanta.anchoredPosition = sceneSantaHomePosition;
    }

    private void SetBlackScreenActive(bool isActive)
    {
        ResolveReferences();
        if (blackScreen == null)
        {
            return;
        }

        if (isActive)
        {
            UpdateBlackScreenLayout();

            // Keep the dimmer over the slot content but under Santa and gifts.
            blackScreen.SetAsLastSibling();
            movingRect?.SetAsLastSibling();
        }

        blackScreen.gameObject.SetActive(isActive);
    }

    private void LateUpdate()
    {
        if (blackScreen != null && blackScreen.gameObject.activeSelf)
        {
            UpdateBlackScreenLayout();
        }
    }

    private void UpdateBlackScreenLayout()
    {
        bool isPortrait = IsPortraitMode();
        blackScreen.anchorMin = new Vector2(0.5f, 0.5f);
        blackScreen.anchorMax = new Vector2(0.5f, 0.5f);
        blackScreen.pivot = new Vector2(0.5f, 0.5f);
        blackScreen.anchoredPosition = Vector2.zero;
        blackScreen.sizeDelta = isPortrait
            ? new Vector2(2000f, 3500f)
            : new Vector2(1920f, 1080f);
        blackScreen.localScale = Vector3.one;
    }

    private static bool IsPortraitMode()
    {
        return Screen.height > Screen.width;
    }

    private void ResolveReferences()
    {
        if (movingRect == null)
        {
            movingRect = GetComponent<RectTransform>();
        }

        if (santaAnimation == null)
        {
            santaAnimation = GetComponent<ImageAnimation>();
        }

        if (giftParent == null && movingRect != null)
        {
            giftParent = movingRect.parent as RectTransform;
        }

        if (blackScreen == null && giftParent != null)
        {
            blackScreen = giftParent.Find("BlackScreen") as RectTransform;
        }

        if (sceneSanta == null && giftParent?.parent != null)
        {
            sceneSanta = giftParent.parent.Find("Santa") as RectTransform;
        }

        if (sceneSanta != null && !hasSceneSantaHomePosition)
        {
            sceneSantaHomePosition = sceneSanta.anchoredPosition;
            hasSceneSantaHomePosition = true;
        }
    }
}
