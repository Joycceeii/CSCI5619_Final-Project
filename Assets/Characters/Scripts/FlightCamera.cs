// Simple chase camera for the WukongFlight scene: stays at a side/front offset from the target (in the target's yaw frame).
using UnityEngine;

public class FlightCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(2.2f, 0.7f, 2.0f);   // x = right, y = up, z = forward of the target's heading
    public Vector3 lookOffset = new Vector3(0, 0.35f, 0);
    public float smooth = 3f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 fwd = target.forward; fwd.y = 0;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        var yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        Vector3 want = target.position + yaw * offset;
        float k = 1 - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Time.frameCount < 3 ? want : Vector3.Lerp(transform.position, want, k);
        transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position, Vector3.up);
    }

    public void Snap()
    {
        if (!target) return;
        Vector3 fwd = target.forward; fwd.y = 0;
        var yaw = Quaternion.LookRotation(fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized, Vector3.up);
        transform.position = target.position + yaw * offset;
        transform.rotation = Quaternion.LookRotation(target.position + lookOffset - transform.position, Vector3.up);
    }
}
