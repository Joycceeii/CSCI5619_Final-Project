// Wukong: switch between "standing, staff in the right hand" and "riding the staff" (flying a looping path with a cloud trail).
// - Toggle: the `flying` checkbox, the on-screen toggle (top-left), or the F key.
// - Stand: Animator state "Stand" (wukong_stand_idle), staff stays a child of RightHand.
// - Ride:  Animator state "Ride" (wukong_ride_idle); the staff leaves the hand, swings under the hips and is parented to Hips;
//          left hand is pinned to the staff with IK; the rig flies a looping path, bobbing up and down; clouds trail behind.
// Staff ride transform / grip come from _models/ride.py (wukong_ride_attach.json), written into the fields by the editor setup.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class WukongFlight : MonoBehaviour
{
    [Header("Switch")]
    public bool flying = true;
    public bool showToggleButton = true;

    [Header("Rig (filled by Tools > Characters > Build WukongFlight)")]
    public Animator animator;
    public Transform character;                 // wukong_rigged instance (child of this object)
    public string staffName = "wukong_staff";
    [Tooltip("Ride pose of the staff = staffRideDelta * (rest pose of the staff), both in character-root space")]
    public Matrix4x4 staffRideDelta = Matrix4x4.identity;
    [Tooltip("Left wrist position in the ride pose, character-root space (IK target, carried by the staff)")]
    public Vector3 leftWristRoot = new Vector3(-0.028f, 0.294f, 0.105f);
    public ParticleSystem cloudTrail, cloudSeat;

    [Header("Flight path (around this object's position)")]
    public float radiusX = 5f, radiusZ = 3.2f;
    public float height = 2.2f;
    public float waveHeight = 0.6f;              // slow up/down along the loop
    public float speed = 2.6f;                   // m/s
    public float bobAmplitude = 0.08f, bobFrequency = 0.7f;
    public float bankAngle = 14f;
    public float takeOffTime = 1.6f;
    public float staffSwapTime = 0.55f;
    [Tooltip("take-off clip: crouch first, then leave the ground / grab the staff")]
    public float takeOffLiftDelay = 0.6f, takeOffStaffDelay = 0.5f;
    public float staffFlickDegrees = 150f, staffFlickLift = 0.12f;

    [Header("Walking (when not flying; W key)")]
    public bool walking = false;
    public float walkSpeed = 0.34f;              // matches the walk clip stride (no foot sliding)
    public float walkRadius = 1.2f;

    Transform staff, rightHand, hips;
    Vector3 restLocalPos; Quaternion restLocalRot;
    Matrix4x4 restRelRoot, rideRelRoot;
    Vector3 wristInStaff;
    bool haveHipsLocal; Vector3 hipsLocalPos; Quaternion hipsLocalRot;
    float fly01, flyVel, staffW, theta, ikW;
    Vector3 homePos; Quaternion homeRot;
    Vector3 groundPos; Quaternion groundRot; float groundTheta, flyHeld;
    static readonly int FlyingHash = Animator.StringToHash("Flying"), WalkingHash = Animator.StringToHash("Walking");

    void Awake()
    {
        if (!character && animator) character = animator.transform;
        if (!animator && character) animator = character.GetComponent<Animator>();
        staff = FindDeep(character, staffName);
        hips = animator ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (!staff || !hips) { Debug.LogWarning("[WukongFlight] staff or Hips not found"); enabled = false; return; }
        rightHand = staff.parent;
        restLocalPos = staff.localPosition; restLocalRot = staff.localRotation;
        // bind pose: staff relative to the character root
        restRelRoot = character.worldToLocalMatrix * staff.localToWorldMatrix;
        rideRelRoot = staffRideDelta * restRelRoot;
        wristInStaff = rideRelRoot.inverse.MultiplyPoint3x4(leftWristRoot);
        homePos = character.localPosition; homeRot = character.localRotation;
        var relay = character.GetComponent<WukongFlightIK>(); if (!relay) relay = character.gameObject.AddComponent<WukongFlightIK>();
        relay.owner = this;
        fly01 = staffW = flying ? 1 : 0;
        flyHeld = flying ? 10f : 0f;
        groundPos = homePos; groundRot = homeRot;
        theta = 0;
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (!t) return null;
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
        return null;
    }

    [ContextMenu("Toggle flying")] public void Toggle() => flying = !flying;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Toggle();
#else
        if (Input.GetKeyDown(KeyCode.F)) Toggle();
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame) walking = !walking;
#else
        if (Input.GetKeyDown(KeyCode.W)) walking = !walking;
#endif
        flyHeld = flying ? flyHeld + Time.deltaTime : 0f;
        bool lift = flying && flyHeld >= takeOffLiftDelay;
        animator.SetBool(FlyingHash, flying);
        animator.SetBool(WalkingHash, walking && !flying);
        fly01 = Mathf.SmoothDamp(fly01, lift ? 1 : 0, ref flyVel, takeOffTime * 0.5f);
        if (walking && !flying && fly01 < 0.01f)
        {
            // stroll a circle on the ground around the start point
            groundTheta += walkSpeed / walkRadius * Time.deltaTime;
            Vector3 G(float a) => homePos + new Vector3(walkRadius * Mathf.Sin(a), 0, walkRadius * Mathf.Cos(a) - walkRadius);
            groundPos = G(groundTheta);
            var gt = G(groundTheta + 0.01f) - groundPos; gt.y = 0;
            if (gt.sqrMagnitude > 1e-8f) groundRot = Quaternion.Slerp(groundRot, Quaternion.LookRotation(gt.normalized, Vector3.up), 1 - Mathf.Exp(-8 * Time.deltaTime));
        }
        if (flying || fly01 > 0.001f) theta += speed / Mathf.Max(0.1f, 0.5f * (radiusX + radiusZ)) * Time.deltaTime * Mathf.Clamp01(fly01 * 1.5f);

        // path point + tangent (local to this object)
        Vector3 P(float a) => new Vector3(radiusX * Mathf.Sin(a), height + waveHeight * Mathf.Sin(2 * a), radiusZ * Mathf.Cos(a) - radiusZ);
        Vector3 p = P(theta), tan = (P(theta + 0.01f) - p).normalized;
        p.y += bobAmplitude * Mathf.Sin(Time.time * bobFrequency * 2 * Mathf.PI);
        Vector3 flat = new Vector3(tan.x, 0, tan.z).normalized;
        Quaternion face = flat.sqrMagnitude > 0 ? Quaternion.LookRotation(flat, Vector3.up) : homeRot;
        float bank = -bankAngle * Mathf.Sign(radiusX * radiusZ) ;                        // loop turns right -> lean into it
        Quaternion pathRot = face * Quaternion.Euler(-Mathf.Asin(Mathf.Clamp(tan.y, -1, 1)) * Mathf.Rad2Deg * 0.5f, 0, bank);
        float s = fly01 * fly01 * (3 - 2 * fly01);
        character.localPosition = Vector3.Lerp(groundPos, p, s);
        character.localRotation = Quaternion.Slerp(groundRot, pathRot, s);

        SetEmission(cloudTrail, lift && fly01 > 0.3f);
        SetEmission(cloudSeat, flying && flyHeld > takeOffStaffDelay);
    }

    static void SetEmission(ParticleSystem ps, bool on)
    {
        if (!ps) return;
        var e = ps.emission; if (e.enabled != on) e.enabled = on;
        if (on && !ps.isPlaying) ps.Play();
    }

    void LateUpdate()
    {
        staffW = Mathf.MoveTowards(staffW, (flying && flyHeld >= takeOffStaffDelay) ? 1 : 0, Time.deltaTime / Mathf.Max(0.01f, staffSwapTime));
        bool inTransition = animator.IsInTransition(0);
        if (staffW <= 0f)
        {
            if (staff.parent != rightHand) staff.SetParent(rightHand, false);
            staff.localPosition = restLocalPos; staff.localRotation = restLocalRot;
            haveHipsLocal = false;
        }
        else if (staffW >= 1f && !inTransition)
        {
            if (!haveHipsLocal)
            {
                // settle once, in the ride pose: target relative to the character root -> keep it relative to Hips from now on
                var w = character.localToWorldMatrix * rideRelRoot;
                staff.SetParent(hips, true);
                staff.SetPositionAndRotation(w.GetPosition(), w.rotation);
                hipsLocalPos = staff.localPosition; hipsLocalRot = staff.localRotation; haveHipsLocal = true;
            }
            staff.localPosition = hipsLocalPos; staff.localRotation = hipsLocalRot;
        }
        else
        {
            // swing between the hand and the seat
            if (staff.parent != character) staff.SetParent(character, true);
            var hand = rightHand.localToWorldMatrix * Matrix4x4.TRS(restLocalPos, restLocalRot, Vector3.one);
            var ride = character.localToWorldMatrix * rideRelRoot;
            float k = staffW * staffW * (3 - 2 * staffW);
            // small flourish while it changes hands: the staff arcs up and flips over (returns exactly to the seat pose)
            float bump = Mathf.Sin(Mathf.PI * k);
            Vector3 pos = Vector3.Lerp(hand.GetPosition(), ride.GetPosition(), k) + character.up * (staffFlickLift * bump);
            Quaternion rot = Quaternion.AngleAxis(staffFlickDegrees * bump, character.right) * Quaternion.Slerp(hand.rotation, ride.rotation, k);
            staff.SetPositionAndRotation(pos, rot);
            haveHipsLocal = false;
        }
        ikW = Mathf.MoveTowards(ikW, (flying && staffW >= 1f) ? 1 : 0, Time.deltaTime * 3f);
    }

    // called from WukongFlightIK.OnAnimatorIK (the Animator lives on the character object)
    internal void DoIK()
    {
        if (!enabled || !staff) return;
        float w = ikW;
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        if (w > 0)
        {
            Vector3 target = haveHipsLocal ? staff.TransformPoint(wristInStaff)
                                           : (character.localToWorldMatrix * rideRelRoot).MultiplyPoint3x4(wristInStaff);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, target);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w * 0.6f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, character.TransformPoint(new Vector3(-0.35f, 0.35f, 0.05f)));
        }
    }

    void OnGUI()
    {
        if (!showToggleButton) return;
        var r = new Rect(12, 12, 190, 34);
        bool f = GUI.Toggle(r, flying, flying ? "  楠戞椋炶 (F)" : "  绔欑珛鎸佹 (F)", GUI.skin.button);
        if (f != flying) flying = f;
    }
}
