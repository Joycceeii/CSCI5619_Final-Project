// Wukong's tail: the main (keyed-style) motion, evaluated in Unity so it follows the character exactly.
// Same shape design as _models/tail_anim.py (Blender preview): base direction (yaw / elevation) + a curl that grows toward the
// tip (monkey "?", tip hooking outward) + a travelling S-wave; every bone samples the shape a little later than its parent
// (overlapping action: the tip trails the root). Shapes are keyed per Animator state (Stand 5 s loop, Walk 1 s loop,
// TakeOff 1.2 s, Ride 2 s loop) with eased in/out interpolation, and cross-fade with the Animator's own transitions.
// Why not the humanoid clip curves: Unity's humanoid retarget rebuilds the Hips a few degrees differently from Blender,
// and the whole tail hangs off the Hips, so the clip-driven tail drifted 10-20 degrees and folded into the cape.
// TailWag (physics, runs after this) adds a little inertia on top.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
public class TailCurve : MonoBehaviour
{
    public Animator animator;
    public Transform character;
    public string chainPrefix = "Tail_";
    [Range(0, 1)] public float weight = 1f;

    public struct Shape
    {
        public float yaw, elev, curl, tip, sAmp, sPhase;
        public static Shape Lerp(Shape a, Shape b, float w) => new Shape
        {
            yaw = Mathf.Lerp(a.yaw, b.yaw, w), elev = Mathf.Lerp(a.elev, b.elev, w), curl = Mathf.Lerp(a.curl, b.curl, w),
            tip = Mathf.Lerp(a.tip, b.tip, w), sAmp = Mathf.Lerp(a.sAmp, b.sAmp, w), sPhase = Mathf.Lerp(a.sPhase, b.sPhase, w)
        };
        public static Shape LerpUnclamped(Shape a, Shape b, float w) => new Shape
        {
            yaw = Mathf.LerpUnclamped(a.yaw, b.yaw, w), elev = Mathf.LerpUnclamped(a.elev, b.elev, w), curl = Mathf.LerpUnclamped(a.curl, b.curl, w),
            tip = Mathf.LerpUnclamped(a.tip, b.tip, w), sAmp = Mathf.LerpUnclamped(a.sAmp, b.sAmp, w), sPhase = Mathf.LerpUnclamped(a.sPhase, b.sPhase, w)
        };
        public Shape(float yaw, float elev, float curl, float tip, float sAmp, float sPhase)
        { this.yaw = yaw; this.elev = elev; this.curl = curl; this.tip = tip; this.sAmp = sAmp; this.sPhase = sPhase; }
    }

    Transform[] bones; Vector3 tipLocal; Transform root;
    // ------------------------------------------------------------------ shape timelines (degrees; yaw + = toward his right)
    static readonly (float t, Shape s)[] StandKeys =
    {
        (0.0f, new Shape(35, -60, 125, -60, 8, 0.0f)),
        (1.7f, new Shape(50, -54, 112, -75, -10, 1.0f)),
        (3.3f, new Shape(26, -63, 134, -48, 13, 2.0f)),
        (5.0f, new Shape(35, -60, 125, -60, 8, 0.0f)),
    };
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    static Shape Keys((float t, Shape s)[] k, float t)
    {
        if (t <= k[0].t) return k[0].s;
        for (int i = 0; i < k.Length - 1; i++)
            if (t <= k[i + 1].t) { float w = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * (t - k[i].t) / (k[i + 1].t - k[i].t)); return Shape.Lerp(k[i].s, k[i + 1].s, w); }
        return k[k.Length - 1].s;
    }
    public static Shape Stand(float t)
    {
        t = Mathf.Repeat(t, 5f); var p = Keys(StandKeys, t);
        if (t >= 3.6f && t <= 4.2f) p.tip -= 25f * Mathf.Sin(Mathf.PI * Smooth((t - 3.6f) / 0.6f));   // tip flick now and then
        return p;
    }
    public static Shape Walk(float t)
    {
        float ph = 2 * Mathf.PI * Mathf.Repeat(t, 1f);
        return new Shape(35 + 20 * Mathf.Sin(ph - 0.5f), -58 + 6 * Mathf.Cos(2 * ph), 120 + 14 * Mathf.Sin(2 * ph - 1f),
                         -60 + 15 * Mathf.Sin(ph - 1.5f), 16 * Mathf.Sin(ph - 1.2f), 0.6f * Mathf.Sin(ph));
    }
    public static Shape Ride(float t)
    {
        float ph = 2 * Mathf.PI * Mathf.Repeat(t, 2f) / 2f;
        return new Shape(30 + 6 * Mathf.Sin(ph), -6 + 5 * Mathf.Sin(ph + 1), 10 + 6 * Mathf.Sin(ph - 0.7f), -20, 32, -ph);   // ribbon in the wind
    }
    static (float t, Shape s)[] takeKeys;
    public static Shape TakeOff(float t)
    {
        takeKeys ??= new[] { (0f, Stand(0)), (0.17f, new Shape(35, -50, 135, -50, 8, 0)), (0.42f, new Shape(30, -75, 90, -30, 0, 0)),
                             (0.62f, new Shape(20, 20, 40, -20, -25, 0)), (0.88f, new Shape(28, -6, 8, -25, 46, -1.5f)), (1.2f, Ride(0)) };
        return Keys(takeKeys, Mathf.Clamp(t, 0, 1.2f));
    }

    void Awake()
    {
        if (!character) character = transform;
        if (!animator) animator = GetComponent<Animator>();
        var list = new List<Transform>();
        for (int i = 1; i < 40; i++) { var b = Find(character, $"{chainPrefix}{i:00}"); if (!b) break; list.Add(b); }
        bones = list.ToArray();
        if (bones.Length < 2) { enabled = false; return; }
        int n = bones.Length;
        var dir = bones[n - 1].position - bones[n - 2].position;
        tipLocal = bones[n - 1].InverseTransformPoint(bones[n - 1].position + dir);
        root = character;
    }
    static Transform Find(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var r = Find(c, n); if (r) return r; } return null; }

    // shape for a given Animator state at time t (seconds into the state)
    Shape StateShape(AnimatorStateInfo st, float timeOffset)
    {
        float len = Mathf.Max(st.length, 0.01f);
        float t = st.normalizedTime * len - timeOffset;
        if (st.IsName("Walk")) return Walk(t);
        if (st.IsName("Ride")) return Ride(t);
        if (st.IsName("TakeOff")) return TakeOff(Mathf.Max(0, Mathf.Min(st.normalizedTime, 1f) * len - timeOffset));
        return Stand(t);
    }

    Shape Current(float lag)
    {
        if (!animator || !animator.runtimeAnimatorController) return Stand(Time.time - lag);
        var cur = StateShape(animator.GetCurrentAnimatorStateInfo(0), lag);
        if (animator.IsInTransition(0))
        {
            var nxt = StateShape(animator.GetNextAnimatorStateInfo(0), lag);
            float w = Smooth(animator.GetAnimatorTransitionInfo(0).normalizedTime);
            return Shape.Lerp(cur, nxt, w);
        }
        return cur;
    }

    // Blender armature space (x left, y back, z up) -> Unity character space (x right, y up, z forward)
    static Vector3 B2U(Vector3 b) => new Vector3(-b.x, b.z, -b.y);

    static Vector3[] Dirs(int n, Shape p)
    {
        var d = new Vector3[n];
        // Blender Z-up; + yaw toward his right (-X)
        Vector3 d0 = RotB(new Vector3(0, Mathf.Cos(p.elev * Mathf.Deg2Rad), Mathf.Sin(p.elev * Mathf.Deg2Rad)), Vector3.forward, p.yaw);
        Vector3 axis = Vector3.Cross(d0, Vector3.forward).normalized;      // curl toward up, in the tail's own plane
        float pc = 0, yc = 0;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)Mathf.Max(n - 1, 1);
            pc += p.curl / n + p.tip * 3f / n * Smooth((u - 0.55f) / 0.45f);
            yc += p.sAmp * 4f / n * Mathf.Sin(2 * Mathf.PI * u + p.sPhase);
            // Blender rotations are right-handed (counter-clockwise looking down the axis)
            Vector3 v = RotB(d0, axis, pc); v = RotB(v, Vector3.forward, yc);
            d[i] = B2U(v).normalized;
        }
        return d;
    }
    static Vector3 RotB(Vector3 v, Vector3 axis, float deg)   // right-handed rotation (Rodrigues), Blender convention
    {
        float a = deg * Mathf.Deg2Rad; axis = axis.normalized;
        return v * Mathf.Cos(a) + Vector3.Cross(axis, v) * Mathf.Sin(a) + axis * Vector3.Dot(axis, v) * (1 - Mathf.Cos(a));
    }

    public enum Style { Lively, Minimal }
    [Header("Feel")]
    public Style style = Style.Lively;
    [Tooltip("critically damped follow of each bone's direction (s): no jumps on state changes, ~0.8 s to settle")]
    public float followTime = 0.28f;
    Vector3[] smoothDir; bool hasSmooth;

    // ------------------------------------------------------------------ Alert: tail shoots straight up (a guilty "tell")
    [Header("Alert (tail shoots up)")]
    public float alertRise = 0.25f, alertRelease = 1.2f;
    public float tremorDegrees = 2.5f, tremorHz = 3.5f;          // tiny, smooth nervous tremble while held
    public static readonly Shape AlertShape = new Shape(15, 10, 80, -30, 0, 0);   // up behind the back, clear of the cape
    float alertStart = -100f, alertHold, alertIntensity;
    public float AlertWeight { get; private set; }
    /// <summary>Tail snaps up (0.25 s, small overshoot), holds, then eases back down (1.2 s).</summary>
    public void Alert(float hold = 2f, float intensity = 1f)
    {
        // re-trigger while up: keep it up, extend the hold (no second snap)
        if (AlertWeight > 0.5f) { alertHold = Mathf.Max(alertHold, Time.time - alertStart - alertRise + hold); alertIntensity = Mathf.Max(alertIntensity, intensity); return; }
        alertStart = Time.time; alertHold = hold; alertIntensity = Mathf.Clamp01(intensity);
    }
    float AlertEnvelope()
    {
        float t = Time.time - alertStart;
        if (t < 0 || t > alertRise + alertHold + alertRelease) return 0;
        if (t < alertRise) { float x = t / alertRise; const float c = 1.6f; return 1 + (c + 1) * Mathf.Pow(x - 1, 3) + c * Mathf.Pow(x - 1, 2); }   // ease-out-back: overshoot
        if (t < alertRise + alertHold) return 1;
        return 1 - Smooth((t - alertRise - alertHold) / alertRelease);
    }

    Shape Minimal(Shape lively, float t)
    {
        // a calm, nearly static "?" that only breathes: slow 6 s sway, no S-wave, no flicks
        float ph = 2 * Mathf.PI * t / 6f;
        bool ride = animator && (animator.GetCurrentAnimatorStateInfo(0).IsName("Ride") || animator.GetCurrentAnimatorStateInfo(0).IsName("TakeOff"));
        return ride ? new Shape(30 + 4 * Mathf.Sin(ph), -6 + 2 * Mathf.Sin(ph + 1), 12, -20, 6 * Mathf.Sin(ph * 1.5f), -ph)
                    : new Shape(38 + 6 * Mathf.Sin(ph), -58 + 2 * Mathf.Sin(ph + 0.8f), 125 + 4 * Mathf.Sin(ph + 2f), -60, 3 * Mathf.Sin(ph), 0);
    }

    void LateUpdate()
    {
        if (weight <= 0) return;
        int n = bones.Length;
        if (smoothDir == null || smoothDir.Length != n) { smoothDir = new Vector3[n]; hasSmooth = false; }
        var st = animator ? animator.GetCurrentAnimatorStateInfo(0) : default;
        float lag = (animator && st.IsName("Stand")) || !animator ? 0.075f : 0.032f;
        float env = AlertEnvelope(); AlertWeight = Mathf.Clamp01(env) * alertIntensity;
        float wA = env * alertIntensity;
        // snappy follow while shooting up, the usual soft follow otherwise
        float ft = (wA > 0.02f && Time.time - alertStart < alertRise + 0.1f) ? 0.035f : followTime;
        float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, ft));
        for (int i = 0; i < n; i++)
        {
            // overlapping action: bone i uses the shape from (i * lag) seconds ago
            var shape = Current(lag * i);
            if (style == Style.Minimal) shape = Minimal(shape, Time.time - lag * i);
            if (wA > 0)
            {
                var up = AlertShape;
                up.sAmp = tremorDegrees * Mathf.Sin(2 * Mathf.PI * tremorHz * (Time.time - 0.02f * i));    // smooth, tiny
                shape = Shape.LerpUnclamped(shape, up, wA);
            }
            Vector3 target = Dirs(n, shape)[i];                                  // character space
            smoothDir[i] = hasSmooth ? Vector3.Slerp(smoothDir[i], target, k).normalized : target;
            Vector3 want = root.TransformDirection(smoothDir[i]);
            var b = bones[i];
            Vector3 tip = i < n - 1 ? bones[i + 1].position : b.TransformPoint(tipLocal);
            Vector3 cur = tip - b.position;
            if (cur.sqrMagnitude < 1e-10f) continue;
            var q = Quaternion.FromToRotation(cur, want);
            b.rotation = Quaternion.Slerp(Quaternion.identity, q, weight) * b.rotation;
        }
        hasSmooth = true;
    }
}