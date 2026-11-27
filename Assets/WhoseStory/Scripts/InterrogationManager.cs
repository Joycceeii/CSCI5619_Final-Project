using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhoseStory
{
    // 按住手柄扳机（编辑器里也可按空格）开始录音，松开后问向玩家正看着的那个角色。
    public class InterrogationManager : MonoBehaviour
    {
        [Header("接线")]
        public OpenAiClient client;
        public MicrophoneCapture microphone;
        public Transform playerHead;
        public InterrogationCharacter[] characters;

        [Header("说话键")]
        [Tooltip("自定义说话键。留空则用默认绑定：左右手柄扳机，外加键盘空格作编辑器备用。")]
        public InputActionProperty talkAction;

        [Header("看向谁")]
        [Tooltip("视线与角色方向的夹角在这个度数以内才算看着他")]
        public float maxAngle = 40f;

        [Header("调试")]
        [Tooltip("在画面上显示状态，并提供一个直接打字提问的输入框")]
        public bool showOverlay = true;

        bool waiting;
        string status = "idle";
        string lastCharacter = "";
        string lastQuestion = "";
        string lastReply = "";
        bool lastLied;
        string typed = "Where were you at half past nine?";
        int typedTarget;

        // 实际在用的说话键：面板上配了绑定就用面板的，否则用下面造的默认动作
        InputAction talk;
        bool ownsTalk;

        InputAction ResolveTalkAction()
        {
            InputAction configured = talkAction.action;
            if (configured != null && configured.bindings.Count > 0)
            {
                return configured;
            }

            // 场景里序列化下来的是一个没有绑定的空动作，所以这里自己造一个
            // 扳机按过一半算按住（Input System 默认按压阈值 0.5）
            var action = new InputAction("Talk", InputActionType.Button);
            action.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            action.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            action.AddBinding("<XRController>{RightHand}/trigger");
            action.AddBinding("<XRController>{LeftHand}/trigger");
            action.AddBinding("<Keyboard>/space");
            ownsTalk = true;
            return action;
        }

        void OnEnable()
        {
            talk ??= ResolveTalkAction();
            talk.Enable();
        }

        void OnDisable()
        {
            talk?.Disable();
        }

        void OnDestroy()
        {
            if (ownsTalk)
            {
                talk?.Dispose();
            }
        }

        // 面板上那个按住录音的按钮，绕开键盘焦点
        bool overlayHeld;

        bool TalkHeld()
        {
            if (overlayHeld)
            {
                return true;
            }

            return talk != null && talk.IsPressed();
        }

        // 玩家现在看着的角色（CharacterPuppet 用它决定谁转身）。问话等回答期间不换人
        public InterrogationCharacter Focused { get; private set; }

        // 其它场景（如五庄观后园）用来接表演反应：问题发出时、回答说完时
        public static event System.Action<InterrogationCharacter, string> QuestionPut;
        public static event System.Action<InterrogationCharacter, string, string, bool> AnswerGiven;

        void Update()
        {
            if (!waiting && playerHead != null)
            {
                Focused = FacedCharacter();
            }

            if (waiting)
            {
                return;
            }

            bool held = TalkHeld();

            if (held && !microphone.IsRecording)
            {
                microphone.Begin();
                status = "recording";
            }
            else if (!held && microphone.IsRecording)
            {
                byte[] wav = microphone.End();
                if (wav == null)
                {
                    status = "too short, nothing sent";
                    return;
                }

                StartCoroutine(RunTurn(wav));
            }
        }

        IEnumerator RunTurn(byte[] wav)
        {
            waiting = true;

            status = "transcribing";
            string question = null;
            yield return client.Transcribe(wav, t => question = t);

            if (string.IsNullOrWhiteSpace(question))
            {
                status = "heard nothing";
                waiting = false;
                yield break;
            }

            yield return Put(question, FacedCharacter());
            waiting = false;
        }

        IEnumerator AskTyped(string question, InterrogationCharacter target)
        {
            waiting = true;
            yield return Put(question, target);
            waiting = false;
        }

        IEnumerator Put(string question, InterrogationCharacter target)
        {
            if (target == null)
            {
                status = "nobody in front of you";
                yield break;
            }

            lastQuestion = question;
            lastCharacter = target.characterName;
            status = $"asking {target.characterName}";

            QuestionPut?.Invoke(target, question);

            yield return target.Ask(client, question);

            lastReply = target.LastReply;
            lastLied = target.LastLied;
            AnswerGiven?.Invoke(target, question, lastReply, lastLied);
            status = "idle";
        }

        InterrogationCharacter FacedCharacter()
        {
            InterrogationCharacter best = null;
            float bestAngle = maxAngle;

            foreach (InterrogationCharacter character in characters)
            {
                Vector3 toCharacter = character.transform.position - playerHead.position;
                float angle = Vector3.Angle(playerHead.forward, toCharacter);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = character;
                }
            }

            return best;
        }

        void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12, 12, 640, 360), GUI.skin.box);

            GUILayout.Label($"status: {status}");

            string talkState = talk == null
                ? "no talk action"
                : (talk.IsPressed() ? $"TALK down ({talk.activeControl?.path})" : "talk up");
            GUILayout.Label($"mic: {microphone.DeviceName}    {talkState}");

            GUI.enabled = !waiting;
            overlayHeld = GUILayout.RepeatButton("Hold here to record", GUILayout.Height(28));
            GUI.enabled = true;

            GUILayout.Space(8);

            GUILayout.Label($"typed question goes to: {characters[typedTarget].characterName}");

            GUILayout.BeginHorizontal();
            for (int i = 0; i < characters.Length; i++)
            {
                string label = typedTarget == i
                    ? "> " + characters[i].characterName + " <"
                    : characters[i].characterName;
                if (GUILayout.Button(label))
                {
                    typedTarget = i;
                }
            }
            GUILayout.EndHorizontal();

            typed = GUILayout.TextField(typed);

            GUI.enabled = !waiting;
            if (GUILayout.Button("Ask"))
            {
                StartCoroutine(AskTyped(typed, characters[typedTarget]));
            }
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label($"answered by: {lastCharacter}");
            GUILayout.Label($"Q: {lastQuestion}");
            GUILayout.Label($"A: {lastReply}");
            GUILayout.Label($"lied: {lastLied}");

            GUILayout.EndArea();
        }
    }
}
