using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("UI/UI Line Connector")]
public class UILineConnector : MonoBehaviour
{
    [Header("Connection Points")]
    [SerializeField] private RectTransform startPoint;
    [SerializeField] private RectTransform midPoint;
    [SerializeField] private RectTransform endPoint;

    [Tooltip("Optional list to connect multiple points. If populated, it overrides the individual Start/Mid/End points.")]
    [SerializeField] private List<RectTransform> customPoints = new List<RectTransform>();

    [Header("Line Styling")]
    [Range(1f, 100f)]
    [SerializeField] private float thickness = 5f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private Sprite lineSprite;
    [SerializeField] private Image.Type imageType = Image.Type.Simple;
    [SerializeField] private Material lineMaterial;

    [Header("Blending Settings")]
    [Range(0f, 1f)]
    [Tooltip("Extends the segment ends slightly beyond the points to overlap and close sharp angle gaps.")]
    [SerializeField] private float overlapExtension = 0.15f;

    [Header("Smooth Anti-Aliasing (Procedural UI Image)")]
    [Tooltip("Use the Procedural UI Image component already in your project to draw perfectly smooth, anti-aliased lines.")]
    [SerializeField] private bool useProceduralImage = true;

    [Header("Update Options")]
    [SerializeField] private bool updateInEditor = true;
    [SerializeField] private bool updateAtRuntime = true;
    [Tooltip("When checked in the Editor Inspector, this component will be removed from the GameObject while leaving spawned child segments intact.")]
    [SerializeField] private bool isDone = false;

    private List<Image> activeSegments = new List<Image>();
    private RectTransform rectTransform;

    public RectTransform StartPoint { get => startPoint; set { startPoint = value; RebuildLine(); } }
    public RectTransform MidPoint { get => midPoint; set { midPoint = value; RebuildLine(); } }
    public RectTransform EndPoint { get => endPoint; set { endPoint = value; RebuildLine(); } }
    public float Thickness { get => thickness; set { thickness = value; RebuildLine(); } }
    public Color LineColor { get => lineColor; set { lineColor = value; UpdateSegmentStyles(); } }
    public Sprite LineSprite { get => lineSprite; set { lineSprite = value; UpdateSegmentStyles(); } }
    public Image.Type ImageType { get => imageType; set { imageType = value; UpdateSegmentStyles(); } }
    public Material LineMaterial { get => lineMaterial; set { lineMaterial = value; UpdateSegmentStyles(); } }
    public float OverlapExtension { get => overlapExtension; set { overlapExtension = value; RebuildLine(); } }
    public bool UseProceduralImage { get => useProceduralImage; set { useProceduralImage = value; RebuildLine(); } }
    public bool IsDone { get => isDone; set => isDone = value; }

    private void OnEnable()
    {
        if (isDone) return;
        rectTransform = GetComponent<RectTransform>();
        RebuildLine();
    }

    private void Update()
    {
        if (isDone) return;
        if (!Application.isPlaying && !updateInEditor) return;
        if (Application.isPlaying && !updateAtRuntime) return;

        RebuildLine();
    }

    private void OnValidate()
    {
        if (isDone)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(this);
                }
            };
#endif
            return;
        }

        RebuildLine();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (isDone) return;
        if (!Application.isPlaying && updateInEditor)
        {
            RebuildLine();
        }
    }
#endif

    public void RebuildLine()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // Collect active points
        List<RectTransform> pts = GetActivePoints();
        if (pts.Count < 2)
        {
            ClearSegments();
            return;
        }

        int neededSegments = pts.Count - 1;
        EnsureSegmentCount(neededSegments);

        // Position and scale line segments
        for (int i = 0; i < neededSegments; i++)
        {
            RectTransform pA = pts[i];
            RectTransform pB = pts[i + 1];

            if (pA == null || pB == null) continue;

            Vector3 localA = rectTransform.InverseTransformPoint(pA.position);
            Vector3 localB = rectTransform.InverseTransformPoint(pB.position);

            Vector2 direction = localB - localA;
            float distance = direction.magnitude;

            // If overlap extension is enabled, push the start and end of the segment slightly outward
            if (overlapExtension > 0f && distance > 0.001f)
            {
                float extLength = thickness * overlapExtension;
                Vector2 dirNormal = direction.normalized;
                localA -= (Vector3)(dirNormal * extLength);
                localB += (Vector3)(dirNormal * extLength);
                direction = localB - localA;
                distance = direction.magnitude;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Image img = activeSegments[i];
            RectTransform segRect = img.rectTransform;

            segRect.pivot = new Vector2(0f, 0.5f);
            segRect.anchorMin = new Vector2(0.5f, 0.5f);
            segRect.anchorMax = new Vector2(0.5f, 0.5f);

            segRect.localPosition = localA;
            segRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            segRect.sizeDelta = new Vector2(distance, thickness);
            segRect.localScale = Vector3.one;
        }

        UpdateSegmentStyles();
    }

    private List<RectTransform> GetActivePoints()
    {
        List<RectTransform> pts = new List<RectTransform>();
        if (customPoints != null && customPoints.Count > 0)
        {
            foreach (var p in customPoints)
            {
                if (p != null) pts.Add(p);
            }
        }
        else
        {
            if (startPoint != null) pts.Add(startPoint);
            if (midPoint != null) pts.Add(midPoint);
            if (endPoint != null) pts.Add(endPoint);
        }
        return pts;
    }

    private void EnsureSegmentCount(int count)
    {
        List<Image> existingChildren = new List<Image>();
        foreach (Transform child in transform)
        {
            Image img = child.GetComponent<Image>();
            if (img != null && child.name.StartsWith("Segment_"))
            {
                existingChildren.Add(img);
            }
        }

        // Re-create segments if the component type changed (Image vs ProceduralImage)
        bool typeMismatch = false;
        foreach (var img in existingChildren)
        {
            bool isProcedural = img is UnityEngine.UI.ProceduralImage.ProceduralImage;
            if (isProcedural != useProceduralImage)
            {
                typeMismatch = true;
                break;
            }
        }

        if (typeMismatch)
        {
            foreach (var img in existingChildren)
            {
                if (img != null)
                {
                    if (Application.isPlaying) Destroy(img.gameObject);
                    else DestroyImmediate(img.gameObject);
                }
            }
            existingChildren.Clear();
        }

        // Destroy extra segments
        while (existingChildren.Count > count)
        {
            Image extra = existingChildren[existingChildren.Count - 1];
            existingChildren.RemoveAt(existingChildren.Count - 1);
            if (extra != null)
            {
                if (Application.isPlaying) Destroy(extra.gameObject);
                else DestroyImmediate(extra.gameObject);
            }
        }

        // Spawn missing segments
        System.Type imageComponentType = useProceduralImage ? typeof(UnityEngine.UI.ProceduralImage.ProceduralImage) : typeof(Image);
        while (existingChildren.Count < count)
        {
            GameObject newSeg = new GameObject($"Segment_{existingChildren.Count}", typeof(RectTransform), imageComponentType);
            newSeg.transform.SetParent(this.transform, false);

            Image img = newSeg.GetComponent<Image>();
            if (useProceduralImage)
            {
                var procImg = img as UnityEngine.UI.ProceduralImage.ProceduralImage;
                procImg.ModifierType = typeof(UniformModifier);
                var uniform = procImg.GetComponent<UniformModifier>();
                if (uniform != null)
                {
                    uniform.Radius = 0f; // Perfect rectangle, but anti-aliased by ProceduralImage's shader falloff
                }
            }
            existingChildren.Add(img);
        }

        activeSegments = existingChildren;
    }

    private void UpdateSegmentStyles()
    {
        // Update line segments
        if (activeSegments != null)
        {
            for (int i = 0; i < activeSegments.Count; i++)
            {
                Image img = activeSegments[i];
                if (img == null) continue;

                img.color = lineColor;
                img.sprite = lineSprite;
                img.type = imageType;
                img.material = lineMaterial;
                img.raycastTarget = false;
            }
        }
    }

    private void ClearSegments()
    {
        if (activeSegments == null) return;
        foreach (var seg in activeSegments)
        {
            if (seg != null)
            {
                if (Application.isPlaying) Destroy(seg.gameObject);
                else DestroyImmediate(seg.gameObject);
            }
        }
        activeSegments.Clear();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying && !isDone)
        {
            ClearSegments();
        }
    }
}

