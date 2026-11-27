// WuzhuangGarden view modes (M = cycle in Play mode). Built by GardenSetup (Tools > Garden > Build WuzhuangGarden).
//   Rebuilt   : courtyard rebuilt from the Jimeng pieces (paving, hall, walls, corridor, tree cards); splat only beyond the walls
//   Hybrid    : Atlas splat + 2.5D photo layer + paving
//   PureSplat : Atlas splat only
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
public class GardenView : MonoBehaviour
{
    public enum Mode { PureSplat, Hybrid, Rebuilt }
    public Mode mode = Mode.Rebuilt;
    public GameObject splatFull, splatHybrid, splatFar;
    [Tooltip("Rebuilt: show the blurry Atlas splat beyond the walls (off = clean skybox + distant mountains only)")]
    public bool showFarSplat = false;
    [Tooltip("2.5D photo layer: only in Hybrid")]
    public GameObject[] hybridOnly;
    [Tooltip("paving: Hybrid and Rebuilt")]
    public GameObject[] pavingObjects;
    [Tooltip("Jimeng rebuild: only in Rebuilt")]
    public GameObject[] rebuiltOnly;

    Mode applied = (Mode)(-1);

    public GameObject ActiveSplat => mode == Mode.Hybrid ? splatHybrid : mode == Mode.Rebuilt ? (showFarSplat ? splatFar : null) : splatFull;   // null: nothing to wait for

    public void Apply()
    {
        if (splatFull) splatFull.SetActive(mode == Mode.PureSplat);
        if (splatHybrid) splatHybrid.SetActive(mode == Mode.Hybrid);
        if (splatFar) splatFar.SetActive(mode == Mode.Rebuilt && showFarSplat);
        if (hybridOnly != null) foreach (var g in hybridOnly) if (g) g.SetActive(mode == Mode.Hybrid);
        if (pavingObjects != null) foreach (var g in pavingObjects) if (g) g.SetActive(mode != Mode.PureSplat);
        if (rebuiltOnly != null) foreach (var g in rebuiltOnly) if (g) g.SetActive(mode == Mode.Rebuilt);
        applied = mode;
    }

    void OnEnable() => Apply();

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Application.isPlaying && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            mode = (Mode)(((int)mode + 1) % 3);
#endif
        if (mode != applied) Apply();
    }
}
