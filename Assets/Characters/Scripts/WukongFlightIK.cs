// Relay: OnAnimatorIK is only sent to scripts on the Animator's GameObject.
using UnityEngine;

public class WukongFlightIK : MonoBehaviour
{
    public WukongFlight owner;
    void OnAnimatorIK(int layerIndex) { if (owner) owner.DoIK(); }
}
