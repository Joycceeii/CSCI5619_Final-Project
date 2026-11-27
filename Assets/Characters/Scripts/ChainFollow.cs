// Soft secondary motion for a short bone chain (Bajie's ears): each bone eases toward its animated direction with a
// critically damped follow (no spring, so no overshoot, no oscillation, no flipping), plus a tiny idle flap and an
// external swing input used by SuspectTells (ear flap / droop). One driver per bone; runs after the Animator.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class ChainFollow : MonoBehaviour
{
    public string chainPrefix = "Ear_L_";
    public Transform character;
    [Tooltip("lag of the chain behind the head (s): bigger = floppier")]
    public float followTime = 0.12f;
    [Tooltip("character-space axis the swing rotates around (ears sticking out sideways: forward = flap up/down)")]
    public Vector3 swingAxis = Vector3.forward;
    [Tooltip("+1 / -1 so the same positive swing lifts both ears")]
    public float side = 1f;
    public float idleFlapDegrees = 2f, idleFlapHz = 0.18f;
    [Tooltip("set by other scripts (SuspectTells): extra swing in degrees, spread along the chain")]
    public float externalSwing = 0f;

    Transform[] bones; Vector3 tipLocal; Vector3[] smooth; bool init; float seed;

    void Awake()
    {
        if (!character) character = transform;
        var list = new List<Transform>();
        for (int i = 1; i < 20; i++) { var t = Find(transform, $"{chainPrefix}{i:00}"); if (!t) break; list.Add(t); }
        bones = list.ToArray();
        if (bones.Length == 0) { enabled = false; return; }
        int n = bones.Length;
        Vector3 dir = n > 1 ? bones[n - 1].position - bones[n - 2].position : bones[n - 1].up * 0.03f;
        tipLocal = bones[n - 1].InverseTransformPoint(bones[n - 1].position + dir);
        smooth = new Vector3[n]; seed = Random.value * 10f;
    }
    static Transform Find(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var r = Find(c, n); if (r) return r; } return null; }

    void LateUpdate()
    {
        int n = bones.Length;
        float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, followTime));
        Vector3 axis = character.TransformDirection(swingAxis).normalized;
        float idle = idleFlapDegrees * Mathf.Sin(2 * Mathf.PI * idleFlapHz * Time.time + seed);
        for (int i = 0; i < n; i++)
        {
            var b = bones[i];
            Vector3 tip = i < n - 1 ? bones[i + 1].position : b.TransformPoint(tipLocal);
            Vector3 cur = tip - b.position;
            if (cur.sqrMagnitude < 1e-10f) continue;
            float u = (i + 1f) / n;
            Vector3 target = Quaternion.AngleAxis(side * (idle + externalSwing) * u / n * 2f, axis) * cur.normalized;
            Vector3 local = character.InverseTransformDirection(target);
            smooth[i] = init ? Vector3.Slerp(smooth[i], local, k).normalized : local;
            b.rotation = Quaternion.FromToRotation(cur, character.TransformDirection(smooth[i])) * b.rotation;
        }
        init = true;
    }
}
