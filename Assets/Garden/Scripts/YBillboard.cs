// Turns a flat card (tree) about the world Y axis so it always faces the active camera:
// walking around it never shows the card edge-on. The card's pivot must be at its centre; it rotates in place.
using UnityEngine;

[ExecuteAlways]
public class YBillboard : MonoBehaviour
{
    [Tooltip("The quad's front faces -forward (Unity Quad): look direction is from the camera to the card")]
    public bool quadFacing = true;

    void LateUpdate()
    {
        var cam = Camera.main; if (!cam) return;
        Vector3 d = transform.position - cam.transform.position; d.y = 0;
        if (d.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(quadFacing ? d.normalized : -d.normalized, Vector3.up);
    }
}
