// "Tells" for a suspect under interrogation: involuntary body reactions that give the player a hint.
// Wukong: when the player's question touches a sensitive topic (the ginseng-fruit case at Wuzhuang Temple), his tail shoots up,
// he flinches a little (shoulders up, head back), then it slowly settles.
//
// Hooks (all lead to TriggerTell):
//   OnPlayerQuestion(text)              - player's question as text. Typed, or the speech-to-text transcript: when the voice
//                                         pipeline has the transcript (InterrogationManager, after client.Transcribe), call
//                                         GetComponent<SuspectTells>()?.OnPlayerQuestion(question) with the same string.
//   OnDialogueResult(reply, emotionTag) - for the language model: ask it to add a field to its JSON reply, e.g.
//                                             {"reply": "...", "lied": false, "tell": "tail_up", "emotion": "nervous"}
//                                         and pass "tell" (or "emotion") here. "tail_up", "nervous", "guilty", "lying",
//                                         "startled" trigger the tail; empty / "calm" do nothing. lied == true can be passed
//                                         as emotionTag "lying".
//   TriggerTell(type, intensity)        - direct call (cut-scenes, scripted moments, debugging).
// Keyword list and cooldown are editable in the Inspector. Test in CharacterTest: type a question in the box at the bottom
// of the Game view and press Enter, or press Y to trigger the tell directly.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(120)]      // after the Animator, before TailCurve (150)
public class SuspectTells : MonoBehaviour
{
    [Header("Who reacts")]
    [Tooltip("what this character does: tail_up (Wukong), ear_flap (Bajie: ears flap twice, then droop + a gulp), grip_freeze (Shaseng: grips the weapon, looks down, freezes)")]
    public string defaultTell = "tail_up";
    public TailCurve tail;
    public Animator animator;
    [Tooltip("ear_flap: the ear chains (ChainFollow on Ear_L_ / Ear_R_)")]
    public ChainFollow[] ears;
    public float flapDegrees = 38f, droopDegrees = 34f;
    [Tooltip("grip_freeze")]
    public float lookDownDegrees = 12f, gripDegrees = 14f;

    [Header("Sensitive topics (substring match, case-insensitive)")]
    public List<string> sensitiveWords = new List<string>
    { "人参果", "果子", "偷", "后园", "园子", "树", "推倒", "镇元", "清风", "明月", "那天晚上", "昨晚" };
    [Tooltip("words that never count (e.g. a question about a different tree)")]
    public List<string> ignoreWords = new List<string>();
    public float cooldown = 4f;
    public float holdSeconds = 2.2f;

    [Header("Body flinch (optional)")]
    public bool bodyFlinch = true;
    public float shrugDegrees = 7f, headBackDegrees = 6f;

    [Header("Test UI (CharacterTest)")]
    public bool showTestBox = false;
    public string displayName = "悟空";
#if ENABLE_INPUT_SYSTEM
    public Key testKey = Key.Y;
    string testKeyName => testKey.ToString();
#else
    public KeyCode testKey = KeyCode.Y;
    string testKeyName => testKey.ToString();
#endif
    [Tooltip("only one character shows the test box; the others just listen to it")]
    public SuspectTells[] alsoAsk;

    public event Action<string, string> TellTriggered;    // (tell type, matched word / tag)
    public string LastMatch { get; private set; } = "";

    float lastTell = -100f;
    string typed = "";
    static readonly HashSet<string> NervousTags = new HashSet<string> { "tail_up", "nervous", "guilty", "lying", "startled", "afraid" };

    void Awake()
    {
        if (!tail) tail = GetComponent<TailCurve>();
        if (!animator) animator = GetComponent<Animator>();
    }

    // ------------------------------------------------------------------ inputs
    /// <summary>Player's question (typed or speech-to-text). Returns true if it triggered a tell.</summary>
    public bool OnPlayerQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.ToLowerInvariant();
        if (ignoreWords.Any(w => !string.IsNullOrEmpty(w) && t.Contains(w.ToLowerInvariant()))) return false;
        string hit = sensitiveWords.FirstOrDefault(w => !string.IsNullOrEmpty(w) && t.Contains(w.ToLowerInvariant()));
        if (hit == null) return false;
        return TriggerTell(defaultTell, 1f, hit);
    }

    /// <summary>Language-model result: reply text + an emotion/tell tag (see the header comment for the JSON field).</summary>
    public bool OnDialogueResult(string replyText, string emotionTag)
    {
        if (string.IsNullOrEmpty(emotionTag)) return false;
        string tag = emotionTag.Trim().ToLowerInvariant();
        return NervousTags.Contains(tag) && TriggerTell(defaultTell, tag == "startled" ? 1f : 0.85f, tag);
    }

    /// <summary>Fire a tell now (respects the cooldown). tellType: "tail_up" for now.</summary>
    public bool TriggerTell(string tellType = null, float intensity = 1f) => TriggerTell(tellType ?? defaultTell, intensity, "manual");

    bool TriggerTell(string tellType, float intensity, string why)
    {
        if (Time.time - lastTell < cooldown) return false;
        lastTell = Time.time; LastMatch = why;
        if (tellType == "tail_up" && tail) tail.Alert(holdSeconds, intensity);
        else { tellStart = Time.time; tellKind = tellType; tellIntensity = Mathf.Clamp01(intensity); }
        TellTriggered?.Invoke(tellType, why);
        Debug.Log($"[SuspectTells] {name}: {tellType} ({why})");
        return true;
    }

    // ------------------------------------------------------------------ body reactions (after the Animator; one extra layer, eased envelopes)
    string tellKind = ""; float tellStart = -100f, tellIntensity = 1f; float savedSpeed = -1f;
    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    /// <summary>0..1 envelope of the current ear/freeze tell: quick in (0.25 s), hold, slow out (1.2 s).</summary>
    float TellEnvelope(out float t)
    {
        t = Time.time - tellStart;
        if (t < 0 || t > 0.25f + holdSeconds + 1.2f) return 0;
        if (t < 0.25f) return Smooth01(t / 0.25f);
        if (t < 0.25f + holdSeconds) return 1;
        return 1 - Smooth01((t - 0.25f - holdSeconds) / 1.2f);
    }
    void Rot(HumanBodyBones b, Vector3 axisLocalToRoot, float deg)
    {
        if (!animator || Mathf.Abs(deg) < 1e-4f) return;
        var t = animator.GetBoneTransform(b); if (!t) return;
        t.rotation = Quaternion.AngleAxis(deg, transform.TransformDirection(axisLocalToRoot)) * t.rotation;
    }
    void LateUpdate()
    {
        // Wukong: shoulders up + head back, following the tail's alert envelope
        if (bodyFlinch && tail && tail.AlertWeight > 0.001f)
        {
            float w = tail.AlertWeight;
            Rot(HumanBodyBones.LeftShoulder, Vector3.forward, -shrugDegrees * w);     // shoulders up (left bone points -X)
            Rot(HumanBodyBones.RightShoulder, Vector3.forward, shrugDegrees * w);
            Rot(HumanBodyBones.Head, Vector3.right, -headBackDegrees * w);             // head pulls back
        }
        float env = TellEnvelope(out float tt) * tellIntensity;
        if (tellKind == "ear_flap")
        {
            // two quick flaps (first 0.7 s), then the ears droop for the hold; the ear springs smooth all of it
            float flap = tt < 0.7f ? Mathf.Sin(2 * Mathf.PI * tt / 0.35f) * (1 - tt / 0.7f) * flapDegrees : 0f;
            float droop = -droopDegrees * env * Smooth01((tt - 0.5f) / 0.4f);
            if (ears != null)
                foreach (var e in ears)
                {
                    if (!e) continue;
                    e.externalSwing = flap * tellIntensity + droop;            // ChainFollow.side mirrors left/right
                }
            // gulp: a small dip of the head + shoulders up around 0.8 s
            float gulp = Mathf.Sin(Mathf.PI * Smooth01((tt - 0.6f) / 0.6f)) * tellIntensity;
            Rot(HumanBodyBones.Head, Vector3.right, 7f * gulp);
            Rot(HumanBodyBones.LeftShoulder, Vector3.forward, -4f * env);
            Rot(HumanBodyBones.RightShoulder, Vector3.forward, 4f * env);
            if (env <= 0 && tt > 1f) { foreach (var e in ears) if (e) e.externalSwing = 0; tellKind = ""; }
        }
        else if (tellKind == "grip_freeze")
        {
            // he goes still (the idle almost stops), looks down, both hands clench (wrists curl in), shoulders tense
            if (animator)
            {
                if (savedSpeed < 0) savedSpeed = animator.speed;
                animator.speed = Mathf.Lerp(savedSpeed, 0.08f, env);
            }
            Rot(HumanBodyBones.Head, Vector3.right, lookDownDegrees * env);
            Rot(HumanBodyBones.Neck, Vector3.right, 0.4f * lookDownDegrees * env);
            Rot(HumanBodyBones.RightHand, Vector3.right, gripDegrees * env);
            Rot(HumanBodyBones.LeftHand, Vector3.right, gripDegrees * env);
            Rot(HumanBodyBones.LeftShoulder, Vector3.forward, -3f * env);
            Rot(HumanBodyBones.RightShoulder, Vector3.forward, 3f * env);
            if (env <= 0 && tt > 1f) { if (animator && savedSpeed >= 0) animator.speed = savedSpeed; savedSpeed = -1f; tellKind = ""; }
        }
    }
    // ------------------------------------------------------------------ test input
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame && !textFocused) TriggerTell();
#else
        if (Input.GetKeyDown(testKey) && !textFocused) TriggerTell();
#endif
    }
    bool textFocused;
    string feedback = "";
    void OnGUI()
    {
        if (!showTestBox) return;
        var r = new Rect(20, Screen.height - 60, Mathf.Min(700, Screen.width - 40), 36);
        GUI.SetNextControlName("suspect_q");
        if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "suspect_q")
        {
            bool hit = OnPlayerQuestion(typed);
            if (alsoAsk != null) foreach (var o in alsoAsk) if (o && o != this) hit |= o.OnPlayerQuestion(typed);
            feedback = hit ? $"露馅了（命中：{LastMatch}）" : (Time.time - lastTell < cooldown ? "冷却中" : "没有反应");
            typed = ""; Event.current.Use();
        }
        typed = GUI.TextField(r, typed, 80);
        textFocused = GUI.GetNameOfFocusedControl() == "suspect_q";
        GUI.Label(new Rect(r.x, r.y - 24, r.width, 22), $"盘问{displayName}（输入问题回车；{testKeyName} = 直接触发）  " + feedback);
    }
}
