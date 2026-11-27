// Test helper: press a key (default T) to make the character say a line through CharacterVoice.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class VoiceTestKey : MonoBehaviour
{
    public CharacterVoice voice;
    public AudioClip line;
#if ENABLE_INPUT_SYSTEM
    public Key key = Key.T;
#else
    public KeyCode key = KeyCode.T;
#endif
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool down = Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
#else
        bool down = Input.GetKeyDown(key);
#endif
        if (down) Say();
    }
    [ContextMenu("Say line")] public void Say() { if (voice && line) voice.PlayLine(line); }
}
