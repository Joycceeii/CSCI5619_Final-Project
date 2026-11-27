// Demo driver: turns a bone (e.g. Bajie's Head) left/right so spring bones (TailWag on the ears) visibly react.
// Off by default; the tail/ear capture switches it on. Runs before TailWag (execution order 100 < 200).
using UnityEngine;

[DefaultExecutionOrder(100)]
public class HeadTurnDemo : MonoBehaviour
{
    public Transform bone;
    public Vector3 axis = Vector3.up;      // world axis
    public float degrees = 28f, period = 2.2f;
    Quaternion restLocal; bool have;
    void LateUpdate()
    {
        if (!bone) return;
        if (!have) { restLocal = bone.localRotation; have = true; }
        bone.localRotation = restLocal;    // no Animator on the test characters -> start from the bind pose every frame
        float s = Mathf.Sin(2 * Mathf.PI * Time.time / period);
        bone.rotation = Quaternion.AngleAxis(degrees * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.5f), axis) * bone.rotation;   // snappy turns
    }
}
