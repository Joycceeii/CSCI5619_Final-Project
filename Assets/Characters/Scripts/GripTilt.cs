// Tilts a hand that holds a rigidly skinned weapon (Wukong's staff is skinned 100 % to RightHand) after the Animator,
// so the staff leans out from the body and slightly forward instead of hanging straight down along the leg.
// The rig has no finger bones (the fist is modelled), so the grip itself cannot be closed further at runtime.
using UnityEngine;

[DefaultExecutionOrder(150)]
public class GripTilt : MonoBehaviour
{
    public Transform hand;
    public Transform character;
    [Tooltip("degrees the weapon top leans away from the body (around the character's forward axis; + = toward the hand's side)")]
    public float outward = 12f;
    [Tooltip("degrees the weapon top leans forward (around the character's right axis)")]
    public float forward = 16f;
    [Tooltip("+1 for a right hand, -1 for a left hand")]
    public float side = 1f;

    void Reset() { character = transform; }

    void LateUpdate() => Apply();

    public void Apply()
    {
        if (!hand) return;
        if (!character) character = transform;
        var q = Quaternion.AngleAxis(forward, character.right) * Quaternion.AngleAxis(side * outward, character.forward);
        hand.rotation = q * hand.rotation;
    }
}
