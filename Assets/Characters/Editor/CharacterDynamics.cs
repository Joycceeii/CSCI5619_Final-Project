// Adds / configures TailWag (tail + ear secondary motion) on a character instance. Used by CharacterTestSetup and WukongFlightSetup.
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterDynamics
{
    public static void Add(GameObject model, string key, WukongFlight flight = null)
    {
        foreach (var old in model.GetComponents<TailWag>()) Object.DestroyImmediate(old);
        var anim = model.GetComponent<Animator>();
        Transform Bone(string n) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        if (key == "wukong")
        {
            // the tail's main motion lives in the keyframed clips: give a bare (CharacterTest) Wukong the standing idle
            if (anim && !anim.runtimeAnimatorController)
            {
                const string cp = "Assets/Characters/Wukong/Flight/WukongStand.controller";
                var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Wukong/Animations/wukong_stand_idle.fbx").OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
                if (clip)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(cp);
                    if (!ctrl) { ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cp); ctrl.layers[0].stateMachine.AddState("Stand").motion = clip; }
                    anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
                }
            }
            foreach (var oldc in model.GetComponents<TailCurve>()) Object.DestroyImmediate(oldc);
            var tc = model.AddComponent<TailCurve>(); tc.animator = anim; tc.character = model.transform;   // main tail motion
            // one driver per bone: TailWag on the tail made it flicker (its spring + the curve fought each other) -> disabled
            var tw = model.AddComponent<TailWag>(); tw.enabled = false;
            tw.chainPrefix = "Tail_"; tw.character = model.transform; tw.flight = flight;
            tw.swingAxis = Vector3.up; tw.liftAxis = Vector3.right;
            // v5: the main tail motion is keyframed in the clips (tail_anim.py); TailWag only adds inertia (physics layer)
            tw.swingDegrees = 0f; tw.liftDegrees = 0f; tw.frequency = 0.3f; tw.phaseDelay = 0.85f; tw.perBoneFalloff = 0.95f; tw.driftDegrees = 0f;
            tw.stiffness = 0.45f; tw.damping = 0.40f; tw.maxAngle = 5f;                     // light follow-through on turns / acceleration
            tw.flyStraighten = 0f; tw.flyDirection = new Vector3(0.45f, 0.1f, -1f);   // back + toward his right: straight back runs through the cape (tail_clip_report)
            tw.flyFlutterDegrees = 0f; tw.flyFlutterFrequency = 0.6f;
            tw.boneRadius = 0.02f; tw.collideFromBone = 99;   // v5: no collision pushes (they curled the keyed tail around the staff/legs); keyed shapes keep clear
            // colliders: legs, staff, cape/back (offsets measured in the bind pose, character space -> bone space)
            Vector3 Back(Transform b, float back, float up = 0) => b.InverseTransformVector(model.transform.TransformVector(new Vector3(0, up, -back)));
            void Cap(string a, string b, float r, float backA = 0, float backB = 0, float upA = 0, float upB = 0)
            {
                var ta = Bone(a); var tb = b != null ? Bone(b) : null; if (!ta) return;
                tw.colliders.Add(new TailWag.Capsule { a = ta, aOffset = Back(ta, backA, upA), b = tb, bOffset = tb ? Back(tb, backB, upB) : Vector3.zero, radius = r });
            }
            foreach (var s in new[] { "Left", "Right" })
            {
                Cap($"{s}UpLeg", $"{s}Leg", 0.05f);
                Cap($"{s}Leg", $"{s}Foot", 0.042f);
            }
            // (no back/cape capsule any more: the keyed tail shapes keep clear of the cape)
            var staff = Bone("wukong_staff");
            var mf = staff ? staff.GetComponent<MeshFilter>() : null;
            if (mf && mf.sharedMesh)
            {
                var bnd = mf.sharedMesh.bounds; Vector3 e = bnd.extents;
                Vector3 ax = e.x > e.y && e.x > e.z ? Vector3.right * e.x : e.y > e.z ? Vector3.up * e.y : Vector3.forward * e.z;
                tw.colliders.Add(new TailWag.Capsule { a = staff, aOffset = bnd.center - ax, b = staff, bOffset = bnd.center + ax, radius = 0.026f });
            }
            EditorUtility.SetDirty(tw);
        }
        else if (key == "bajie")
        {
            // ears: critically damped follow (no spring -> no flicker); SuspectTells drives the flap / droop through externalSwing
            foreach (var old in model.GetComponents<ChainFollow>()) Object.DestroyImmediate(old);
            foreach (var side in new[] { "L", "R" })
            {
                var cf = model.AddComponent<ChainFollow>();
                cf.chainPrefix = $"Ear_{side}_"; cf.character = model.transform; cf.swingAxis = Vector3.forward;
                cf.side = side == "L" ? -1f : 1f;           // + swing lifts both ears
                cf.followTime = 0.12f; cf.idleFlapDegrees = 2f; cf.idleFlapHz = 0.18f;
                EditorUtility.SetDirty(cf);
            }
        }
    }
}
