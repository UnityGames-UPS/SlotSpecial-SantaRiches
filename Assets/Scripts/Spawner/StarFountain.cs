using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class StarFountain : MonoBehaviour
{
    [Header("Star Fountain Settings")]
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private Transform starSpawnContainer;
    [SerializeField] private int starPoolSize = 200;

    [Header("Behavior Settings")]
    [Tooltip("If true, items maintain initial scale and alpha throughout travel (fade variation 0-10% max). Useful for coin fountains.")]
    [SerializeField] private bool disableEndFadeAndScale = false;
    [Tooltip("Keeps each item's original transform rotation throughout the burst.")]
    [SerializeField] private bool disableItemRotation = false;

    [Header("Center Coin Shower")]
    [SerializeField, Min(1)] private int coinShowerMin = 100;
    [SerializeField, Min(1)] private int coinShowerMax = 100;
    [SerializeField, Min(0f)] private float coinShowerSpawnJitter = 0.35f;

    private readonly List<GameObject> starPool = new List<GameObject>();
    private int activeBurstStars;

    private void Start()
    {
        InitializeStarPool();
    }

    private void InitializeStarPool()
    {
        if (starPrefab == null || starPool.Count > 0) return;
        Transform parentContainer = starSpawnContainer != null ? starSpawnContainer : transform;

        for (int i = 0; i < starPoolSize; i++)
        {
            GameObject star = Instantiate(starPrefab, parentContainer);
            star.SetActive(false);
            starPool.Add(star);
        }
    }

    private GameObject GetPooledStar()
    {
        for (int i = 0; i < starPool.Count; i++)
        {
            if (starPool[i] != null && !starPool[i].activeSelf)
            {
                return starPool[i];
            }
        }

        Transform parentContainer = starSpawnContainer != null ? starSpawnContainer : transform;
        if (starPrefab != null)
        {
            GameObject star = Instantiate(starPrefab, parentContainer);
            star.SetActive(false);
            starPool.Add(star);
            return star;
        }

        return null;
    }

    internal void PlayStarBurst()
    {
        StopStarBurst();
        if (starPrefab == null) return;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        InitializeStarPool();

        // Keep the original opening density, but release it as one center burst
        // instead of pre-warming particles mid-flight and spawning continuously.
        int burstCount = Random.Range(25, 40);
        for (int i = 0; i < burstCount; i++)
        {
            if (SpawnSingleBurstStar())
            {
                activeBurstStars++;
            }
        }

        if (activeBurstStars == 0)
        {
            DeactivateFountain();
        }
    }

    internal void StopStarBurst()
    {
        activeBurstStars = 0;

        for (int i = 0; i < starPool.Count; i++)
        {
            if (starPool[i] != null)
            {
                RecycleStar(starPool[i]);
            }
        }
    }

    private void RecycleStar(GameObject star)
    {
        if (star == null) return;

        DOTween.Kill(star);
        DOTween.Kill(star.transform);
        RectTransform rt = star.GetComponent<RectTransform>();
        if (rt != null) DOTween.Kill(rt);

        CanvasGroup cg = star.GetComponent<CanvasGroup>();
        Image img = star.GetComponent<Image>();
        if (cg != null) DOTween.Kill(cg);
        if (img != null) DOTween.Kill(img);

        star.SetActive(false);

        if (cg != null) cg.alpha = 0f;
        if (img != null)
        {
            Color c = img.color;
            c.a = 0f;
            img.color = c;
        }

        if (rt != null) rt.anchoredPosition = Vector2.zero;
        else star.transform.localPosition = Vector3.zero;
        star.transform.localRotation = Quaternion.identity;
    }

    private void OnBurstStarComplete(GameObject star)
    {
        RecycleStar(star);

        if (activeBurstStars > 0)
        {
            activeBurstStars--;
        }

        if (activeBurstStars == 0)
        {
            DeactivateFountain();
        }
    }

    private void DeactivateFountain()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private Vector2 GetRandomEdgePosition()
    {
        Vector2 bounds = GetFountainBounds();
        float width = bounds.x;
        float height = bounds.y;

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float horizontalDistance = Mathf.Abs(cos) > 0.0001f ? halfWidth / Mathf.Abs(cos) : float.MaxValue;
        float verticalDistance = Mathf.Abs(sin) > 0.0001f ? halfHeight / Mathf.Abs(sin) : float.MaxValue;
        float distanceToBorder = Mathf.Min(horizontalDistance, verticalDistance);

        return new Vector2(cos * distanceToBorder, sin * distanceToBorder);
    }

    private Vector2 GetFountainBounds()
    {
        float width = 800f;
        float height = 600f;
        RectTransform boundsRect = (starSpawnContainer != null ? starSpawnContainer : transform) as RectTransform;

        // The fountain object itself can be only 100x100. Use the largest parent
        // UI bounds so particles can enter and leave beyond the popup edges.
        while (boundsRect != null)
        {
            if (boundsRect.rect.width > width) width = boundsRect.rect.width;
            if (boundsRect.rect.height > height) height = boundsRect.rect.height;
            boundsRect = boundsRect.parent as RectTransform;
        }

        return new Vector2(width, height);
    }

    private bool SpawnSingleBurstStar()
    {
        GameObject star = GetPooledStar();
        if (star == null) return false;

        RecycleStar(star);

        Vector2 startOffset = Vector2.zero;
        RectTransform starRect = star.GetComponent<RectTransform>();
        if (starRect != null)
        {
            starRect.anchoredPosition = startOffset;
        }
        else
        {
            star.transform.localPosition = startOffset;
        }

        float randomScale = Random.Range(0.3f, 1.2f);
        star.transform.localScale = Vector3.one * randomScale;

        float randomRotation = disableItemRotation ? 0f : Random.Range(0f, 360f);
        star.transform.localRotation = Quaternion.Euler(0f, 0f, randomRotation);

        CanvasGroup cg = star.GetComponent<CanvasGroup>();
        Image img = star.GetComponent<Image>();

        // For coins (disableEndFadeAndScale), alpha variation is 0-10% max (0.9 to 1.0) and stays constant
        float startAlpha = disableEndFadeAndScale ? Random.Range(0.9f, 1.0f) : 1f;

        if (cg != null) cg.alpha = startAlpha;
        if (img != null)
        {
            Color c = img.color;
            c.a = startAlpha;
            img.color = c;
        }

        Vector2 targetPos = GetRandomEdgePosition();

        float animDuration = Random.Range(1.4f, 2.2f);

        star.SetActive(true);

        if (!disableEndFadeAndScale)
        {
            if (cg != null) cg.DOFade(0f, animDuration).SetEase(Ease.Linear);
            else if (img != null) img.DOFade(0f, animDuration).SetEase(Ease.Linear);
        }

        Sequence starSeq = DOTween.Sequence();
        if (starRect != null)
        {
            starSeq.Join(starRect.DOAnchorPos(targetPos, animDuration).From(startOffset).SetEase(Ease.Linear));
        }
        else
        {
            starSeq.Join(star.transform.DOLocalMove(targetPos, animDuration).From(startOffset).SetEase(Ease.Linear));
        }

        if (!disableItemRotation)
        {
            float extraRot = Random.Range(-90f, 90f);
            starSeq.Join(star.transform.DORotate(
                new Vector3(0, 0, randomRotation + extraRot),
                animDuration,
                RotateMode.FastBeyond360));
        }

        if (!disableEndFadeAndScale)
        {
            starSeq.Join(star.transform.DOScale(randomScale * 0.4f, animDuration * 0.4f).SetDelay(animDuration * 0.6f));
        }

        starSeq.OnComplete(() => OnBurstStarComplete(star));
        return true;
    }

    internal void PlayCenterCoinShower(float showerDuration)
    {
        StopStarBurst();
        if (starPrefab == null) return;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        InitializeStarPool();
        int minimum = Mathf.Max(1, coinShowerMin);
        int maximum = Mathf.Max(minimum, coinShowerMax);
        int coinCount = Random.Range(minimum, maximum + 1);
        const float maximumTravelDuration = 2.2f;
        // Keep releasing new coins for the complete popup presentation. Coins
        // launched near the end are recycled when UIManager closes the popup.
        float spawnWindow = Mathf.Max(0f, showerDuration);
        float spawnInterval = coinCount > 0 ? spawnWindow / coinCount : 0f;
        for (int i = 0; i < coinCount; i++)
        {
            float spawnDelay = spawnInterval * i;
            if (spawnInterval > 0f)
            {
                spawnDelay += Random.Range(0f, Mathf.Min(spawnInterval, coinShowerSpawnJitter));
            }

            float travelDuration = Random.Range(1.8f, maximumTravelDuration);
            if (SpawnSingleCenterCoin(i % 3, spawnDelay, travelDuration))
            {
                activeBurstStars++;
            }
        }

        if (activeBurstStars == 0)
        {
            DeactivateFountain();
        }
    }

    private bool SpawnSingleCenterCoin(int directionIndex, float spawnDelay, float duration)
    {
        GameObject coin = GetPooledStar();
        if (coin == null) return false;

        RecycleStar(coin);
        Vector2 bounds = GetFountainBounds();
        Vector2 startPosition = new Vector2(
            Random.Range(-bounds.x * 0.025f, bounds.x * 0.025f),
            Random.Range(bounds.y * 0.02f, bounds.y * 0.1f));
        float horizontalDirection = directionIndex == 0 ? -1f : directionIndex == 2 ? 1f : 0f;
        float horizontalVelocity = horizontalDirection == 0f
            ? Random.Range(-bounds.x * 0.035f, bounds.x * 0.035f)
            : horizontalDirection * Random.Range(bounds.x * 0.25f, bounds.x * 0.38f);
        float verticalVelocity = -Random.Range(bounds.y * 0.05f, bounds.y * 0.14f);
        float gravity = -Random.Range(bounds.y * 0.35f, bounds.y * 0.55f);
        float elapsed = 0f;

        RectTransform coinRect = coin.GetComponent<RectTransform>();
        if (coinRect != null) coinRect.anchoredPosition = startPosition;
        else coin.transform.localPosition = startPosition;

        coin.transform.localScale = Vector3.one * Random.Range(0.55f, 1.05f);
        coin.transform.localRotation = Quaternion.identity;
        CanvasGroup canvasGroup = coin.GetComponent<CanvasGroup>();
        Image image = coin.GetComponent<Image>();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (image != null)
        {
            Color color = image.color;
            color.a = 0f;
            image.color = color;
        }

        coin.SetActive(true);
        Sequence coinSequence = DOTween.Sequence()
            .SetTarget(coin)
            .SetUpdate(true);
        coinSequence.AppendInterval(Mathf.Max(0f, spawnDelay));
        coinSequence.AppendCallback(() =>
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (image != null)
            {
                Color color = image.color;
                color.a = 1f;
                image.color = color;
            }
        });
        coinSequence.Append(DOTween.To(
                () => elapsed,
                value =>
                {
                    elapsed = value;
                    Vector2 position = startPosition + new Vector2(
                        horizontalVelocity * value,
                        verticalVelocity * value + 0.5f * gravity * value * value);
                    if (coinRect != null) coinRect.anchoredPosition = position;
                    else coin.transform.localPosition = position;
                },
                duration,
                duration)
            .SetEase(Ease.Linear));
        coinSequence.OnComplete(() => OnBurstStarComplete(coin));
        return true;
    }

    private void OnDisable()
    {
        StopStarBurst();
    }
}
