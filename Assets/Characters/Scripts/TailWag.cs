// Procedural secondary motion for a bone chain (Wukong's tail Tail_01..07, Bajie's ears Ear_L/R_01..03).
// Runs in LateUpdate, after the Animator:
//  1) idle wag: per-bone sine, phase delayed from root to tip (side-to-side, plus a little up/down), per-bone falloff
//  2) flight (optional WukongFlight): the chain is pulled out straight behind the character and only flutters
//  3) spring bones: every bone tip is a damped point mass chasing that goal pose -> lag / whip when the character
//     turns, accelerates or moves its head; angle clamp per bone + capsule colliders (legs, staff, cape) against clipping
// If the Animator does not write these bones the bind pose is used as the base, so it also works with no animation.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class TailWag : MonoBehaviour
{
    [System.Serializable]
    public class Capsule
    {
        public Transform a; public Vector3 aOffset;
        public Transform b; public Vector3 bOffset;      // b == null -> sphere around a
        public float radius = 0.05f;
        public Vector3 A => a.TransformPoint(aOffset);
        public Vector3 B => b ? b.TransformPoint(bOffset) : A;
    }

    [Header("Chain")]
    public string chainPrefix = "Tail_";
    public Transform[] bones;                    // root -> tip (auto-filled from chainPrefix when empty)
    public Transform character;                  // frame for the swing axes (default: this transform)
    public Vector3 swingAxis = Vector3.up;       // character space: side-to-side
    public Vector3 liftAxis = Vector3.right;     // character space: up/down

    [Header("Idle wag")]
    public float swingDegrees = 9f;              // per bone, at the root
    public float liftDegrees = 3f;
    public float frequency = 0.55f;              // Hz
    public float phaseDelay = 0.6f;              // radians per bone (wave travels root -> tip)
    [Range(0.3f, 1.5f)] public float perBoneFalloff = 0.92f;

    [Header("Spring (inertia)")]
    [Range(0.01f, 1f)] public float stiffness = 0.22f;   // fraction of the gap closed per 60 Hz step
    [Range(0f, 1f)] public float damping = 0.25f;
    public float gravity = 0f;
    public float maxAngle = 35f;                         // max deviation of a bone from its goal direction

    [Header("Flight (Wukong)")]
    public WukongFlight flight;
    [Range(0, 1)] public float flyStraighten = 0.85f;
    public Vector3 flyDirection = new Vector3(0.45f, 0.1f, -1f);  // character space: back, toward the right (clears the cape)
    public float flyFlutterDegrees = 4f, flyFlutterFrequency = 0.8f;
    [Tooltip("slow random sway of the root bones (Perlin), degrees")] public float driftDegrees = 4f;
    [Tooltip("set by other scripts (e.g. SuspectTells ear flap/droop): extra swing in degrees, spread along the chain; the springs smooth it")]
    public float externalSwing = 0f;

    [Header("Collision")]
    public List<Capsule> colliders = new List<Capsule>();
    public float boneRadius = 0.02f;
    public int collideFromBone = 1;

    Quaternion[] rest, lastWritten, goalLocal;
    Vector3[] sim, simPrev;
    Vector3 tipLocal;
    float[] segLen;
    float flyW, phase, seed; bool driven;
    bool init;

    void Awake()
    {
        if (!character) character = transform;
        seed = Random.value * 100f;
        var an = GetComponentInParent<Animator>();
        driven = GetComponent<TailCurve>() != null || (an && an.runtimeAnimatorController);
        if (bones == null || bones.Length == 0)
        {
            var list = new List<Transform>();
            for (int i = 1; i < 20; i++)
            {
                var t = FindDeep(transform, $"{chainPrefix}{i:00}");
                if (!t) break; list.Add(t);
            }
            bones = list.ToArray();
        }
        int n = bones.Length;
        if (n == 0) { enabled = false; return; }
        rest = new Quaternion[n]; lastWritten = new Quaternion[n]; goalLocal = new Quaternion[n];
        sim = new Vector3[n]; simPrev = new Vector3[n]; segLen = new float[n];
        for (int i = 0; i < n; i++) { rest[i] = bones[i].localRotation; lastWritten[i] = rest[i]; }
        for (int i = 0; i < n - 1; i++) segLen[i] = Vector3.Distance(bones[i].position, bones[i + 1].position);
        // virtual tip for the last bone: continue the previous segment's direction
        Vector3 dir = n > 1 ? (bones[n - 1].position - bones[n - 2].position) : bones[n - 1].up * 0.05f;
        segLen[n - 1] = n > 1 ? segLen[n - 2] : 0.05f;
        tipLocal = bones[n - 1].InverseTransformPoint(bones[n - 1].position + dir.normalized * segLen[n - 1]);
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
        return null;
    }

    Vector3 Tip(int i) => i < bones.Length - 1 ? bones[i + 1].position : bones[i].TransformPoint(tipLocal);

    void OnDisable()
    {
        if (rest == null) return;
        for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rest[i];
        init = false;
    }

    void LateUpdate()
    {
        int n = bones.Length;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float t = Time.time;

        // ---- base pose: animated if the Animator wrote the bone this frame, else bind pose
        // (only when nothing else drives the chain: with an Animator / TailCurve upstream, a slow pose can equal last frame's
        //  output and must NOT be mistaken for "not animated" - that reset made the tail flicker between two poses)
        if (!driven)
            for (int i = 0; i < n; i++)
                if (Quaternion.Angle(bones[i].localRotation, lastWritten[i]) < 0.01f) bones[i].localRotation = rest[i];

        bool flying = flight && flight.isActiveAndEnabled && flight.flying;
        flyW = Mathf.MoveTowards(flyW, flying ? 1 : 0, dt / 0.8f);
        float fw = flyW * flyW * (3 - 2 * flyW);

        Vector3 wSwing = character.TransformDirection(swingAxis).normalized;
        Vector3 wLift = character.TransformDirection(liftAxis).normalized;
        Vector3 wFly = character.TransformDirection(flyDirection).normalized;
        // slow random drift (Perlin) on amplitude and a lazy offset -> not metronomic
        float drift = Mathf.PerlinNoise(seed, t * 0.11f), drift2 = Mathf.PerlinNoise(seed + 7.3f, t * 0.07f) * 2 - 1;
        float amp = Mathf.Lerp(swingDegrees, flyFlutterDegrees, fw) * (0.7f + 0.6f * drift), lamp = Mathf.Lerp(liftDegrees, flyFlutterDegrees * 1.5f, fw);
        float freq = Mathf.Lerp(frequency, flyFlutterFrequency, fw);
        phase += 2 * Mathf.PI * freq * dt * (0.85f + 0.3f * drift);     // integrate phase: frequency changes never jump

        // ---- goal pose (root first, children inherit)
        for (int i = 0; i < n; i++)
        {
            var b = bones[i];
            if (fw > 0)
            {
                Vector3 cur = Tip(i) - b.position;
                if (cur.sqrMagnitude > 1e-8f)
                    b.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, wFly), fw * flyStraighten) * b.rotation;
            }
            float k = Mathf.Pow(perBoneFalloff, i);
            float ph = phase - i * phaseDelay;
            float side = amp * k * Mathf.Sin(ph) + (i < 3 ? driftDegrees * drift2 * (1 - fw) : 0f) + externalSwing / n;
            b.rotation = Quaternion.AngleAxis(side, wSwing) * Quaternion.AngleAxis(lamp * k * Mathf.Sin(ph * 0.5f + 1.3f), wLift) * b.rotation;
            goalLocal[i] = b.localRotation;
        }

        // ---- springs
        if (!init)
        {
            for (int i = 0; i < n; i++) { sim[i] = simPrev[i] = Tip(i); }
            init = true;
        }
        // fixed 120 Hz sub-steps: same motion at any frame rate, no overshoot from long frames
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 8); float h = dt / steps;
        float kStiff = 1 - Mathf.Pow(1 - stiffness, h * 60f);
        float kKeep = Mathf.Pow(1 - damping, h * 60f);
        for (int i = 0; i < n; i++)
        {
            var b = bones[i];
            b.localRotation = goalLocal[i];                       // goal relative to the (already simulated) parent
            Vector3 head = b.position, goal = Tip(i);
            Vector3 p = sim[i], pp = simPrev[i];
            for (int st = 0; st < steps; st++)
            {
                Vector3 v = (p - pp) * kKeep; pp = p;
                p += v + (goal - p) * kStiff + Vector3.down * gravity * h * h;
                p = head + (p - head).normalized * segLen[i];
            }
            simPrev[i] = pp;
            // length + angle limit
            Vector3 gd = goal - head, d = p - head;
            if (d.sqrMagnitude < 1e-10f) d = gd;
            float ang = Vector3.Angle(gd, d);
            if (ang > maxAngle) d = Vector3.Slerp(gd.normalized, d.normalized, maxAngle / ang);
            d = d.normalized * segLen[i];
            p = head + d;
            if (i >= collideFromBone)
                foreach (var c in colliders)
                {
                    if (c == null || !c.a) continue;
                    Vector3 a = c.A, bb = c.B, ab = bb - a;
                    float s = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
                    Vector3 q = a + ab * s, off = p - q; float r = c.radius + boneRadius;
                    if (off.sqrMagnitude < r * r)
                    {
                        p = q + (off.sqrMagnitude > 1e-10f ? off.normalized : (p - head).normalized) * r;
                        p = head + (p - head).normalized * segLen[i];
                    }
                }
            // never let collisions drag a bone further than maxAngle from its animated direction (prevents run-away flips)
            Vector3 dd = p - head; float a2 = Vector3.Angle(gd, dd);
            if (a2 > maxAngle) { dd = Vector3.Slerp(gd.normalized, dd.normalized, maxAngle / a2) * segLen[i]; p = head + dd; }
            sim[i] = p;
            b.rotation = Quaternion.FromToRotation(gd, p - head) * b.rotation;
        }
        for (int i = 0; i < n; i++) lastWritten[i] = bones[i].localRotation;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        foreach (var c in colliders)
        {
            if (c == null || !c.a) continue;
            Gizmos.DrawWireSphere(c.A, c.radius); Gizmos.DrawWireSphere(c.B, c.radius); Gizmos.DrawLine(c.A, c.B);
        }
    }
}
