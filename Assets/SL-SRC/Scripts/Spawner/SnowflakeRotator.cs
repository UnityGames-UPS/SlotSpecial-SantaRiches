using UnityEngine;

[DisallowMultipleComponent]
public sealed class SnowflakeRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Rotation speed around the local Z axis in degrees per second. Use a negative value to rotate clockwise.")]
    [SerializeField] private float zRotationSpeed = 20f;

    public float ZRotationSpeed => zRotationSpeed;

    public void SetRotationSpeed(float degreesPerSecond)
    {
        zRotationSpeed = degreesPerSecond;
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, zRotationSpeed * Time.deltaTime, Space.Self);
    }
}
