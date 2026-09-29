using UnityEngine;

/// <summary>
/// Host-only replacement for the legacy demo's <c>AxisRotator</c>: keeps something moving in the
/// benchmark and smoke scenes so a frame is never trivially cached.
/// </summary>
public sealed class HostRotator : MonoBehaviour
{
    public Vector3 DegreesPerSecond = new Vector3(0f, 40f, 15f);

    void Update()
    {
        transform.Rotate(DegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
