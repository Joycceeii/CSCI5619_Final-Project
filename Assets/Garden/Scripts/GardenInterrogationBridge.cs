// Connects the WhoseStory interrogation (question -> OpenAI reply -> speech) to the Journey-to-the-West characters:
//  - when a question is put to a suspect, its SuspectTells reacts to sensitive words (Wukong's tail shoots up, ...)
//  - when the suspect starts speaking the reply, the clip is handed to CharacterVoice so uLipSync moves the mouth
//  - a reply marked as a lie triggers the suspect's tell as well
// Used by Assets/Scenes/Interrogation_Garden.unity (built by InterrogationGardenSetup). The original Interrogation.unity is untouched.
using UnityEngine;
using WhoseStory;

public class GardenInterrogationBridge : MonoBehaviour
{
    public InterrogationCharacter[] characters;

    void OnEnable() { InterrogationManager.QuestionPut += OnQuestion; InterrogationManager.AnswerGiven += OnAnswer; }

    // The original game's AccusationController attaches itself to every scene with an InterrogationManager; its
    // solution/ending texts are the mirror case (Snow White / Queen / Pinocchio), so it is removed here until the
    // garden case has its own accusation step.
    void Start()
    {
        foreach (var a in FindObjectsByType<AccusationController>(FindObjectsSortMode.None)) Destroy(a);
        // ...and what it built in Awake: the world-space hint panel and the floor marker (the stray dark square on the floor)
        foreach (var n in new[] { "AccusationPanel", "AccusationMarker" })
            for (var g = GameObject.Find(n); g; g = GameObject.Find(n)) DestroyImmediate(g);
    }
    void OnDisable() { InterrogationManager.QuestionPut -= OnQuestion; InterrogationManager.AnswerGiven -= OnAnswer; }

    public void OnQuestion(InterrogationCharacter c, string question)
    {
        var tells = c ? c.GetComponent<SuspectTells>() : null;
        bool fired = tells && tells.OnPlayerQuestion(question);
        Debug.Log($"[GardenInterrogation] question to {c?.characterName}: \"{question}\" -> tell {(fired ? tells.LastMatch : "none")}");
    }

    void OnAnswer(InterrogationCharacter c, string question, string reply, bool lied)
    {
        Debug.Log($"[GardenInterrogation] {c.characterName} answered (lied={lied}): {reply}");
    }

    void Update()
    {
        if (characters == null) return;
        foreach (var c in characters)
        {
            if (!c) continue;
            var src = c.GetComponent<AudioSource>(); var voice = c.GetComponent<CharacterVoice>();
            if (!src || !voice || !src.isPlaying || !src.clip) continue;
            if (voice.CurrentClip == src.clip) continue;
            // a new reply clip started on the suspect's AudioSource: drive the mouth from it
            voice.PlayLine(src.clip);
            if (c.LastLied) c.GetComponent<SuspectTells>()?.TriggerTell();
        }
    }
}
