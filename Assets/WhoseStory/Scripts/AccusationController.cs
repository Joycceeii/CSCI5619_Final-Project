using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhoseStory
{
    // 指控与结局。审讯中按 A/X（编辑器里 Tab）进入指控：
    //   1. 看向一个角色，按 A 选定（编辑器里也可按 1/2/3）
    //   2. 按住扳机说出理由，松开后转成文字；不满意可以再按住重录
    //   3. 按 A 确认，指控对象和理由写进本局 JSONL 日志（event = "verdict"），然后显示结局
    //   4. 结局里按 A 重新开始（会开一个新的日志文件）
    // B/Y（编辑器里 Backspace）返回上一步。
    // 场景里有 InterrogationManager 就会自动挂上本组件，不需要改场景生成脚本。
    public class AccusationController : MonoBehaviour
    {
        [Tooltip("真正打碎镜子的人，对应 InterrogationCharacter.characterName")]
        public string culpritName = "Snow White";

        [Header("面板摆放")]
        public float panelDistance = 1.2f;

        enum Stage { Interrogating, Choosing, Reason, Ending }

        Stage stage = Stage.Interrogating;
        InterrogationManager manager;
        InterrogationCharacter facing;
        InterrogationCharacter accused;

        bool recording;
        bool transcribing;
        string reason = "";
        string reasonNote = "";

        InputAction openAction;
        InputAction confirmAction;
        InputAction backAction;
        InputAction recordAction;
        InputAction[] pickKeys;

        Canvas canvas;
        RectTransform panel;
        Text label;
        Transform marker;
        float panelDown;
        bool snapPanel;

        // 自动挂载：每次加载场景都检查一遍，重开一局后也照样生效
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var found = FindFirstObjectByType<InterrogationManager>();
            if (found != null && found.GetComponent<AccusationController>() == null
                && FindFirstObjectByType<AccusationController>() == null)
            {
                found.gameObject.AddComponent<AccusationController>();
            }
        }

        static InputAction MakeButton(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string b in bindings)
            {
                action.AddBinding(b);
            }
            action.Enable();
            return action;
        }

        void Awake()
        {
            manager = GetComponent<InterrogationManager>();
            if (manager == null)
            {
                manager = FindFirstObjectByType<InterrogationManager>();
            }

            openAction = MakeButton("Accuse",
                "<XRController>{RightHand}/primaryButton", "<XRController>{LeftHand}/primaryButton",
                "<Keyboard>/tab");
            confirmAction = MakeButton("Confirm",
                "<XRController>{RightHand}/primaryButton", "<XRController>{LeftHand}/primaryButton",
                "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            backAction = MakeButton("Back",
                "<XRController>{RightHand}/secondaryButton", "<XRController>{LeftHand}/secondaryButton",
                "<Keyboard>/backspace", "<Keyboard>/escape");
            recordAction = MakeButton("RecordReason",
                "<XRController>{RightHand}/{TriggerButton}", "<XRController>{LeftHand}/{TriggerButton}",
                "<XRController>{RightHand}/trigger", "<XRController>{LeftHand}/trigger",
                "<Keyboard>/space");
            pickKeys = new[]
            {
                MakeButton("Pick1", "<Keyboard>/1", "<Keyboard>/numpad1"),
                MakeButton("Pick2", "<Keyboard>/2", "<Keyboard>/numpad2"),
                MakeButton("Pick3", "<Keyboard>/3", "<Keyboard>/numpad3"),
            };

            BuildPanel();
            BuildMarker();
            snapPanel = true;
        }

        void OnDestroy()
        {
            openAction?.Dispose();
            confirmAction?.Dispose();
            backAction?.Dispose();
            recordAction?.Dispose();
            if (pickKeys != null)
            {
                foreach (InputAction a in pickKeys) a.Dispose();
            }
        }

        Transform Head => manager != null && manager.playerHead != null
            ? manager.playerHead
            : (Camera.main != null ? Camera.main.transform : transform);

        bool InterrogationIdle()
        {
            if (manager == null) return false;
            if (manager.microphone != null && manager.microphone.IsRecording) return false;
            foreach (InterrogationCharacter c in manager.characters)
            {
                if (c != null && c.IsBusy) return false;
            }
            return true;
        }

        void Update()
        {
            if (manager == null) return;

            // 每帧只做一次状态切换，避免同一下 A 键既打开指控又选定角色
            switch (stage)
            {
                case Stage.Interrogating: UpdateInterrogating(); break;
                case Stage.Choosing: UpdateChoosing(); break;
                case Stage.Reason: UpdateReason(); break;
                case Stage.Ending: UpdateEnding(); break;
            }

            FollowHead();
        }

        void UpdateInterrogating()
        {
            // 审讯模式下松开扳机之后才把审讯交还给 InterrogationManager，免得返回时误触录音
            if (!manager.enabled && !recordAction.IsPressed())
            {
                manager.enabled = true;
            }

            bool idle = manager.enabled && InterrogationIdle();
            ShowPanel(idle
                    ? "Ready to name the culprit?  Press <b>A</b> to accuse."
                    : "<color=#bbbbbb>Press <b>A</b> to accuse once they finish speaking.</color>",
                700, 70, 28, 0.55f);

            if (idle && openAction.WasPressedThisFrame())
            {
                manager.enabled = false;
                GoTo(Stage.Choosing);
            }
        }

        void UpdateChoosing()
        {
            facing = Gazed();
            PlaceMarker(facing);

            string who = facing != null
                ? $"<size=44><b>{facing.characterName}</b></size>\n\nPress <b>A</b> to accuse them."
                : "<color=#bbbbbb>Look at one of the three suspects.</color>";
            ShowPanel("<b>Who broke the mirror?</b>\n\n" + who + "\n\n<size=24><color=#bbbbbb>B: back to questioning</color></size>",
                760, 380, 30, 0.6f);

            for (int i = 0; i < pickKeys.Length && i < manager.characters.Length; i++)
            {
                if (pickKeys[i].WasPressedThisFrame())
                {
                    facing = manager.characters[i];
                    Choose();
                    return;
                }
            }

            if (facing != null && confirmAction.WasPressedThisFrame())
            {
                Choose();
                return;
            }

            if (backAction.WasPressedThisFrame())
            {
                PlaceMarker(null);
                GoTo(Stage.Interrogating);
            }
        }

        void Choose()
        {
            accused = facing;
            reason = "";
            reasonNote = "";
            PlaceMarker(accused);
            GoTo(Stage.Reason);
        }

        void UpdateReason()
        {
            MicrophoneCapture mic = manager.microphone;
            bool held = recordAction.IsPressed();

            if (held && !recording && !transcribing)
            {
                mic.Begin();
                recording = true;
                reasonNote = "";
            }
            else if (!held && recording)
            {
                recording = false;
                byte[] wav = mic.End();
                if (wav == null)
                {
                    reasonNote = "Too short. Hold the trigger while you speak.";
                }
                else
                {
                    StartCoroutine(TranscribeReason(wav));
                }
            }

            string body;
            if (recording)
            {
                body = "<color=#ff6666><b>● Recording…</b></color>\nRelease the trigger when you are done.";
            }
            else if (transcribing)
            {
                body = "<color=#bbbbbb>Listening back…</color>";
            }
            else if (!string.IsNullOrWhiteSpace(reason))
            {
                body = $"<i>\"{reason}\"</i>\n\nPress <b>A</b> to confirm.\n<size=24><color=#bbbbbb>Hold the trigger to say it again.</color></size>";
            }
            else
            {
                body = "Hold the <b>trigger</b> and say why.";
            }

            if (reasonNote.Length > 0)
            {
                body += $"\n<size=24><color=#ffcc66>{reasonNote}</color></size>";
            }

            ShowPanel($"You accuse <b>{accused.characterName}</b>.\n\n{body}\n\n<size=24><color=#bbbbbb>B: choose someone else</color></size>",
                820, 440, 30, 0.6f);

            if (recording || transcribing) return;

            if (confirmAction.WasPressedThisFrame())
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reasonNote = "Give a reason first.";
                    return;
                }
                Commit();
                return;
            }

            if (backAction.WasPressedThisFrame())
            {
                GoTo(Stage.Choosing);
            }
        }

        IEnumerator TranscribeReason(byte[] wav)
        {
            transcribing = true;
            string text = null;
            string error = null;
            yield return Guarded(manager.client.Transcribe(wav, t => text = t), e => error = e);
            transcribing = false;

            if (error != null)
            {
                Debug.LogWarning($"[WhoseStory] reason transcription failed: {error}");
                reasonNote = "Couldn't reach the speech service. Try again.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                reasonNote = "Didn't catch that. Try again.";
            }
            else
            {
                reason = text.Trim();
            }
        }

        // OpenAiClient 出错时直接抛异常；这里逐步推进它并接住异常，免得指控界面卡在“转写中”
        static IEnumerator Guarded(IEnumerator inner, System.Action<string> onError)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (System.Exception e)
                {
                    onError(e.Message);
                    yield break;
                }
                yield return current;
            }
        }

        void Commit()
        {
            if (TranscriptLogger.Instance != null)
            {
                TranscriptLogger.Instance.WriteVerdict(accused.characterName, reason);
            }
            Debug.Log($"[WhoseStory] verdict: {accused.characterName} | {reason}");
            GoTo(Stage.Ending);
        }

        void UpdateEnding()
        {
            ShowPanel(EndingText(accused.characterName), 1100, 820, 28, 0.15f);

            if (confirmAction.WasPressedThisFrame())
            {
                Restart();
            }
        }

        const string Truth =
            "<b>What really happened</b>\n" +
            "At ten past nine the Queen took the mirror down to clean it and left it leaning on a chair. " +
            "She is sure she hung it back. She never did.\n" +
            "At half past nine Snow White came down the stairs with a basket, caught her foot on that chair, " +
            "and the mirror fell and broke. She told no one.\n" +
            "Pinocchio came in through the kitchen at nine in muddy boots, saw the Queen with the mirror, " +
            "heard someone on the stairs, and walked past the broken glass. Then he denied all of it.\n\n" +
            "<b>Their rules</b>\n" +
            "Pinocchio: every fact he gives about last night is false.\n" +
            "The Queen: always sincere, but wrong about the mirror.\n" +
            "Snow White: tells the truth, except for one moment she hides and denies.";

        string EndingText(string name)
        {
            bool correct = name == culpritName;
            string headline;
            if (correct)
            {
                headline = "<size=48><color=#7ddc7d><b>Case closed.</b></color></size>\n" +
                           "Snow White broke the mirror. It was an accident, and she was too frightened to say so.";
            }
            else if (name == "Queen")
            {
                headline = "<size=48><color=#ff8a7a><b>Wrong suspect.</b></color></size>\n" +
                           "The Queen did take the mirror down, and she believes every word she told you. But she did not break it.";
            }
            else if (name == "Pinocchio")
            {
                headline = "<size=48><color=#ff8a7a><b>Wrong suspect.</b></color></size>\n" +
                           "Pinocchio lied about everything, but lying is not breaking. He only walked past the glass.";
            }
            else
            {
                headline = $"<size=48><color=#ff8a7a><b>Wrong suspect.</b></color></size>\n{name} did not break the mirror.";
            }

            return headline + "\n\n<size=24>" + Truth + "</size>\n\n<color=#bbbbbb>Press <b>A</b> to play again.</color>";
        }

        void Restart()
        {
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
#endif
        }

        void GoTo(Stage next)
        {
            stage = next;
            snapPanel = true;
            if (next != Stage.Reason)
            {
                reasonNote = "";
            }
            if (next == Stage.Ending || next == Stage.Interrogating)
            {
                PlaceMarker(next == Stage.Ending ? accused : null);
            }
        }

        // 与 InterrogationManager 一样用视线夹角判断在看谁
        InterrogationCharacter Gazed()
        {
            InterrogationCharacter best = null;
            float bestAngle = manager.maxAngle;
            Transform head = Head;

            foreach (InterrogationCharacter c in manager.characters)
            {
                if (c == null) continue;
                float angle = Vector3.Angle(head.forward, c.transform.position - head.position);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = c;
                }
            }
            return best;
        }

        // ---------- 头显里看得见的面板 ----------

        void BuildPanel()
        {
            var go = new GameObject("AccusationPanel");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            panel = (RectTransform)go.transform;
            panel.localScale = Vector3.one * 0.001f;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.06f, 0.82f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.color = Color.white;
            label.raycastTarget = false;
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(30, 20);
            rt.offsetMax = new Vector2(-30, -20);
        }

        void ShowPanel(string text, float width, float height, int fontSize, float down)
        {
            label.text = text;
            label.fontSize = fontSize;
            panel.sizeDelta = new Vector2(width, height);
            panelDown = down;
        }

        // 面板慢慢跟着头转，停在视线下方，不挡住角色
        void FollowHead()
        {
            Transform head = Head;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = head.up;
            forward.Normalize();

            Vector3 target = head.position + forward * panelDistance + Vector3.down * panelDown;
            float k = snapPanel ? 1f : 1f - Mathf.Exp(-3f * Time.deltaTime);
            panel.position = Vector3.Lerp(panel.position, target, k);
            panel.rotation = Quaternion.LookRotation(panel.position - head.position, Vector3.up);
            snapPanel = false;
        }

        // 被看着或被指控的角色脚下一圈红环
        void BuildMarker()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "AccusationMarker";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                mat.SetColor("_BaseColor", new Color(0.9f, 0.15f, 0.15f));
                ring.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            marker = ring.transform;
            ring.SetActive(false);
        }

        void PlaceMarker(InterrogationCharacter who)
        {
            if (who == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }
            marker.gameObject.SetActive(true);
            // 角色根物体在胸口高度，脚底约低 1 米
            Vector3 p = who.transform.position;
            marker.position = new Vector3(p.x, p.y - 1f + 0.01f, p.z);
        }
    }
}
