using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SnowflakeSpawner : MonoBehaviour
{
    [Header("Snowflake Pools")]
    [Tooltip("Scene objects that use the first snowflake visual.")]
    [SerializeField] private GameObject[] flake1Pool = new GameObject[0];
    [Min(0f)] [SerializeField] private float flake1Weight = 1f;

    [Tooltip("Scene objects that use the second snowflake visual.")]
    [SerializeField] private GameObject[] flake2Pool = new GameObject[0];
    [Min(0f)] [SerializeField] private float flake2Weight = 1f;

    [Tooltip("Scene objects that use the third snowflake visual.")]
    [SerializeField] private GameObject[] flake3Pool = new GameObject[0];
    [Min(0f)] [SerializeField] private float flake3Weight = 1f;

    [Tooltip("Scene objects that use the fourth snowflake visual.")]
    [SerializeField] private GameObject[] flake4Pool = new GameObject[0];
    [Min(0f)] [SerializeField] private float flake4Weight = 1f;

    [Tooltip("Scene objects that use the fifth snowflake visual.")]
    [SerializeField] private GameObject[] flake5Pool = new GameObject[0];
    [Min(0f)] [SerializeField] private float flake5Weight = 1f;

    [Tooltip("Maximum number of snowflakes that may be falling at the same time.")]
    [Min(1)]
    [SerializeField] private int maxActiveSnowflakes = 50;

    [Header("Spawn Area")]
    [Tooltip("Minimum X position from which a snowflake can fall. UI snowflakes use anchored coordinates; other objects use world coordinates.")]
    [SerializeField] private float minSpawnX = -5f;

    [Tooltip("Maximum X position from which a snowflake can fall. UI snowflakes use anchored coordinates; other objects use world coordinates.")]
    [SerializeField] private float maxSpawnX = 5f;

    [Header("Spawn Timing")]
    [Tooltip("Extra delay added between snowflakes when the pool starts.")]
    [Min(0f)]
    [SerializeField] private float initialStagger = 0.15f;

    [Tooltip("Minimum delay before an inactive snowflake is reused.")]
    [Min(0f)]
    [SerializeField] private float minSpawnDelay = 0.1f;

    [Tooltip("Maximum delay before an inactive snowflake is reused.")]
    [Min(0f)]
    [SerializeField] private float maxSpawnDelay = 0.6f;

    [Header("Falling")]
    [Tooltip("Slowest random falling speed. UI snowflakes use pixels per second; other objects use world units per second.")]
    [Min(0f)]
    [SerializeField] private float minFallSpeed = 1f;

    [Tooltip("Fastest random falling speed. UI snowflakes use pixels per second; other objects use world units per second.")]
    [Min(0f)]
    [SerializeField] private float maxFallSpeed = 2f;

    [Tooltip("Minimum number of seconds a snowflake remains active.")]
    [Min(0f)]
    [SerializeField] private float minActiveTime = 5f;

    [Tooltip("Maximum number of seconds a snowflake remains active.")]
    [Min(0f)]
    [SerializeField] private float maxActiveTime = 8f;

    [Tooltip("Deactivate a snowflake when it passes this Y position. UI snowflakes use anchored coordinates; other objects use world coordinates.")]
    [SerializeField] private bool useDespawnY = true;

    [SerializeField] private float despawnY = -6f;

    [Header("Rotation")]
    [Tooltip("Slowest absolute Z-axis rotation speed in degrees per second.")]
    [Min(0f)]
    [SerializeField] private float minRotationSpeed = 10f;

    [Tooltip("Fastest absolute Z-axis rotation speed in degrees per second.")]
    [Min(0f)]
    [SerializeField] private float maxRotationSpeed = 30f;

    private readonly List<SnowflakeState> allSnowflakes = new List<SnowflakeState>();
    private readonly List<RuntimePool> runtimePools = new List<RuntimePool>();
    private readonly List<RuntimePool> readyPools = new List<RuntimePool>();
    private readonly List<SnowflakeState> readySnowflakes = new List<SnowflakeState>();
    private bool isInitialized;

    private sealed class RuntimePool
    {
        public float SelectionWeight;
        public readonly List<SnowflakeState> Snowflakes = new List<SnowflakeState>();
    }

    private sealed class SnowflakeState
    {
        public GameObject GameObject;
        public Transform Transform;
        public RectTransform RectTransform;
        public SnowflakeRotator Rotator;
        public Vector3 OriginalPosition;
        public Vector3 OriginalAnchoredPosition;
        public Quaternion OriginalRotation;
        public float FallSpeed;
        public float ActivateAt;
        public float DeactivateAt;
    }

    private void Awake()
    {
        InitializePools();
    }

    private void OnEnable()
    {
        if (isInitialized)
        {
            SchedulePool();
        }
    }

    private void Update()
    {
        int activeSnowflakeCount = CountActiveSnowflakes();
        int activeSnowflakeLimit = Mathf.Min(Mathf.Max(1, maxActiveSnowflakes), allSnowflakes.Count);

        for (int i = 0; i < allSnowflakes.Count; i++)
        {
            SnowflakeState snowflake = allSnowflakes[i];

            if (!snowflake.GameObject.activeSelf)
            {
                continue;
            }

            MoveSnowflake(snowflake);

            bool lifetimeFinished = Time.time >= snowflake.DeactivateAt;
            bool passedDespawnPoint = useDespawnY && GetCurrentY(snowflake) <= despawnY;

            if (lifetimeFinished || passedDespawnPoint)
            {
                DeactivateSnowflake(snowflake, true);
                activeSnowflakeCount = Mathf.Max(0, activeSnowflakeCount - 1);
            }
        }

        while (activeSnowflakeCount < activeSnowflakeLimit && TryGetRandomReadySnowflake(out SnowflakeState snowflake))
        {
            ActivateSnowflake(snowflake);
            activeSnowflakeCount++;
        }
    }

    private int CountActiveSnowflakes()
    {
        int activeCount = 0;

        for (int i = 0; i < allSnowflakes.Count; i++)
        {
            if (allSnowflakes[i].GameObject.activeSelf)
            {
                activeCount++;
            }
        }

        return activeCount;
    }

    private void OnDisable()
    {
        if (!isInitialized)
        {
            return;
        }

        for (int i = 0; i < allSnowflakes.Count; i++)
        {
            DeactivateSnowflake(allSnowflakes[i], false);
        }
    }

    private void InitializePools()
    {
        allSnowflakes.Clear();
        runtimePools.Clear();
        HashSet<GameObject> addedSnowflakes = new HashSet<GameObject>();

        AddConfiguredPool(flake1Pool, flake1Weight, addedSnowflakes);
        AddConfiguredPool(flake2Pool, flake2Weight, addedSnowflakes);
        AddConfiguredPool(flake3Pool, flake3Weight, addedSnowflakes);
        AddConfiguredPool(flake4Pool, flake4Weight, addedSnowflakes);
        AddConfiguredPool(flake5Pool, flake5Weight, addedSnowflakes);

        isInitialized = true;

        if (allSnowflakes.Count == 0)
        {
            Debug.LogWarning($"[{nameof(SnowflakeSpawner)}] No snowflakes are assigned to any pool.", this);
        }
    }

    private void AddConfiguredPool(
        GameObject[] configuredSnowflakes,
        float selectionWeight,
        HashSet<GameObject> addedSnowflakes)
    {
        if (configuredSnowflakes == null)
        {
            return;
        }

        RuntimePool runtimePool = new RuntimePool
        {
            SelectionWeight = Mathf.Max(0f, selectionWeight)
        };

        for (int i = 0; i < configuredSnowflakes.Length; i++)
        {
            GameObject snowflakeObject = configuredSnowflakes[i];

            if (snowflakeObject == null || !addedSnowflakes.Add(snowflakeObject))
            {
                continue;
            }

            if (snowflakeObject == gameObject || transform.IsChildOf(snowflakeObject.transform))
            {
                Debug.LogWarning(
                    $"[{nameof(SnowflakeSpawner)}] '{snowflakeObject.name}' contains the spawner and cannot be pooled.",
                    this);
                continue;
            }

            SnowflakeState state = CreateSnowflakeState(snowflakeObject);
            runtimePool.Snowflakes.Add(state);
            allSnowflakes.Add(state);
            ResetTransform(state);
            snowflakeObject.SetActive(false);
        }

        if (runtimePool.Snowflakes.Count > 0)
        {
            runtimePools.Add(runtimePool);
        }
    }

    private SnowflakeState CreateSnowflakeState(GameObject snowflakeObject)
    {
        Transform snowflakeTransform = snowflakeObject.transform;
        RectTransform snowflakeRectTransform = snowflakeTransform as RectTransform;
        SnowflakeRotator rotator = snowflakeObject.GetComponent<SnowflakeRotator>();

        if (rotator == null)
        {
            rotator = snowflakeObject.AddComponent<SnowflakeRotator>();
        }

        return new SnowflakeState
        {
            GameObject = snowflakeObject,
            Transform = snowflakeTransform,
            RectTransform = snowflakeRectTransform,
            Rotator = rotator,
            OriginalPosition = snowflakeTransform.position,
            OriginalAnchoredPosition = snowflakeRectTransform != null
                ? snowflakeRectTransform.anchoredPosition3D
                : Vector3.zero,
            OriginalRotation = snowflakeTransform.rotation
        };
    }

    private void SchedulePool()
    {
        float initialSpawnWindow = Mathf.Max(0f, initialStagger) * Mathf.Max(0, allSnowflakes.Count - 1);

        for (int i = 0; i < allSnowflakes.Count; i++)
        {
            SnowflakeState snowflake = allSnowflakes[i];
            ResetTransform(snowflake);
            snowflake.GameObject.SetActive(false);
            snowflake.ActivateAt = Time.time
                + Random.Range(0f, initialSpawnWindow)
                + RandomRange(minSpawnDelay, maxSpawnDelay);
        }
    }

    private bool TryGetRandomReadySnowflake(out SnowflakeState snowflake)
    {
        readyPools.Clear();
        float totalWeight = 0f;

        for (int i = 0; i < runtimePools.Count; i++)
        {
            RuntimePool runtimePool = runtimePools[i];

            if (!HasReadySnowflake(runtimePool))
            {
                continue;
            }

            readyPools.Add(runtimePool);
            totalWeight += runtimePool.SelectionWeight;
        }

        if (readyPools.Count == 0)
        {
            snowflake = null;
            return false;
        }

        RuntimePool selectedPool = totalWeight > 0f
            ? GetWeightedRandomPool(totalWeight)
            : readyPools[Random.Range(0, readyPools.Count)];

        readySnowflakes.Clear();

        for (int i = 0; i < selectedPool.Snowflakes.Count; i++)
        {
            SnowflakeState candidate = selectedPool.Snowflakes[i];

            if (!candidate.GameObject.activeSelf && Time.time >= candidate.ActivateAt)
            {
                readySnowflakes.Add(candidate);
            }
        }

        snowflake = readySnowflakes[Random.Range(0, readySnowflakes.Count)];
        return true;
    }

    private static bool HasReadySnowflake(RuntimePool runtimePool)
    {
        for (int i = 0; i < runtimePool.Snowflakes.Count; i++)
        {
            SnowflakeState snowflake = runtimePool.Snowflakes[i];

            if (!snowflake.GameObject.activeSelf && Time.time >= snowflake.ActivateAt)
            {
                return true;
            }
        }

        return false;
    }

    private RuntimePool GetWeightedRandomPool(float totalWeight)
    {
        float randomWeight = Random.Range(0f, totalWeight);

        for (int i = 0; i < readyPools.Count; i++)
        {
            RuntimePool runtimePool = readyPools[i];
            randomWeight -= runtimePool.SelectionWeight;

            if (randomWeight <= 0f)
            {
                return runtimePool;
            }
        }

        return readyPools[readyPools.Count - 1];
    }

    private void ActivateSnowflake(SnowflakeState snowflake)
    {
        float spawnX = RandomRange(minSpawnX, maxSpawnX);

        if (snowflake.RectTransform != null)
        {
            Vector3 spawnPosition = snowflake.OriginalAnchoredPosition;
            spawnPosition.x = spawnX;
            snowflake.RectTransform.anchoredPosition3D = spawnPosition;
            snowflake.Transform.rotation = snowflake.OriginalRotation;
        }
        else
        {
            Vector3 spawnPosition = snowflake.OriginalPosition;
            spawnPosition.x = spawnX;
            snowflake.Transform.SetPositionAndRotation(spawnPosition, snowflake.OriginalRotation);
        }

        snowflake.FallSpeed = RandomRange(minFallSpeed, maxFallSpeed);
        snowflake.DeactivateAt = Time.time + RandomRange(minActiveTime, maxActiveTime);
        snowflake.Rotator.SetRotationSpeed(GetRandomRotationSpeed());
        snowflake.GameObject.SetActive(true);
    }

    private void DeactivateSnowflake(SnowflakeState snowflake, bool scheduleRespawn)
    {
        snowflake.GameObject.SetActive(false);
        ResetTransform(snowflake);

        if (scheduleRespawn)
        {
            snowflake.ActivateAt = Time.time + RandomRange(minSpawnDelay, maxSpawnDelay);
        }
    }

    private void ResetTransform(SnowflakeState snowflake)
    {
        if (snowflake.RectTransform != null)
        {
            snowflake.RectTransform.anchoredPosition3D = snowflake.OriginalAnchoredPosition;
            snowflake.Transform.rotation = snowflake.OriginalRotation;
            return;
        }

        snowflake.Transform.SetPositionAndRotation(snowflake.OriginalPosition, snowflake.OriginalRotation);
    }

    private static void MoveSnowflake(SnowflakeState snowflake)
    {
        float distance = snowflake.FallSpeed * Time.deltaTime;

        if (snowflake.RectTransform != null)
        {
            Vector3 anchoredPosition = snowflake.RectTransform.anchoredPosition3D;
            anchoredPosition.y -= distance;
            snowflake.RectTransform.anchoredPosition3D = anchoredPosition;
            return;
        }

        snowflake.Transform.position += Vector3.down * distance;
    }

    private static float GetCurrentY(SnowflakeState snowflake)
    {
        return snowflake.RectTransform != null
            ? snowflake.RectTransform.anchoredPosition.y
            : snowflake.Transform.position.y;
    }

    private float GetRandomRotationSpeed()
    {
        float speed = RandomRange(minRotationSpeed, maxRotationSpeed);
        return Random.value < 0.5f ? -speed : speed;
    }

    private static float RandomRange(float firstValue, float secondValue)
    {
        return Random.Range(Mathf.Min(firstValue, secondValue), Mathf.Max(firstValue, secondValue));
    }
}
